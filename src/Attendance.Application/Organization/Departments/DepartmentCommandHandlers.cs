using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;

namespace Attendance.Application.Organization.Departments;

internal sealed class CreateDepartmentCommandHandler(
    IDepartmentRepository departments,
    DepartmentReferenceResolver references,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDepartmentCommand, Result<DepartmentDto>>
{
    public async Task<Result<DepartmentDto>> HandleAsync(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var code = OrgCodes.Normalize(command.Code);
        if (await departments.CodeExistsAsync(code, cancellationToken))
        {
            return DepartmentErrors.CodeTaken(code);
        }

        var parent = await references.ResolveParentAsync(command.ParentDepartmentId, department: null, cancellationToken);
        if (!parent.IsSuccess)
        {
            return parent.Error;
        }

        var head = await references.ResolveHeadAsync(command.HeadEmployeeId, currentHead: null, cancellationToken);
        if (!head.IsSuccess)
        {
            return head.Error;
        }

        var department = new Department
        {
            Code = code,
            Name = command.Name.Trim(),
            ParentDepartment = parent.Value,
            HeadEmployee = head.Value,
        };
        departments.Add(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return department.ToDto();
    }
}

internal sealed class UpdateDepartmentCommandHandler(
    IDepartmentRepository departments,
    DepartmentReferenceResolver references,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateDepartmentCommand, Result<DepartmentDto>>
{
    public async Task<Result<DepartmentDto>> HandleAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var department = await departments.FindAsync(command.PublicId, cancellationToken);
        if (department is null)
        {
            return DepartmentErrors.NotFound;
        }

        var code = OrgCodes.Normalize(command.Code);
        if (code != department.Code && await departments.CodeExistsAsync(code, cancellationToken))
        {
            return DepartmentErrors.CodeTaken(code);
        }

        var parent = await references.ResolveParentAsync(command.ParentDepartmentId, department, cancellationToken);
        if (!parent.IsSuccess)
        {
            return parent.Error;
        }

        var head = await references.ResolveHeadAsync(command.HeadEmployeeId, department.HeadEmployee, cancellationToken);
        if (!head.IsSuccess)
        {
            return head.Error;
        }

        department.Code = code;
        department.Name = command.Name.Trim();
        department.ParentDepartment = parent.Value;
        department.ParentDepartmentId = parent.Value?.Id;
        department.HeadEmployee = head.Value;
        department.HeadEmployeeId = head.Value?.Id;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return department.ToDto();
    }
}

internal sealed class SetDepartmentActiveCommandHandler(
    IDepartmentRepository departments,
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetDepartmentActiveCommand, Result<DepartmentDto>>
{
    public async Task<Result<DepartmentDto>> HandleAsync(SetDepartmentActiveCommand command, CancellationToken cancellationToken)
    {
        var department = await departments.FindAsync(command.PublicId, cancellationToken);
        if (department is null)
        {
            return DepartmentErrors.NotFound;
        }

        if (department.IsActive == command.IsActive)
        {
            return department.ToDto();
        }

        if (!command.IsActive)
        {
            if (await departments.HasActiveChildrenAsync(department.Id, cancellationToken))
            {
                return DepartmentErrors.HasActiveChildren;
            }

            if (await employees.AnyActiveInDepartmentAsync(department.Id, cancellationToken))
            {
                return DepartmentErrors.HasActiveEmployees;
            }
        }

        department.IsActive = command.IsActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return department.ToDto();
    }
}

/// <summary>Resolves and checks the parent department and head employee named by a create or update.</summary>
internal sealed class DepartmentReferenceResolver(
    IDepartmentRepository departments,
    IEmployeeRepository employees,
    EmployeeScopeResolver scopes)
{
    /// <param name="department">The department being updated; null when creating one.</param>
    public async Task<Result<Department?>> ResolveParentAsync(
        Guid? parentId, Department? department, CancellationToken cancellationToken)
    {
        // An unchanged parent stays as it is, even if it has since been retired.
        if (department is not null && parentId == department.ParentDepartment?.PublicId)
        {
            return Result<Department?>.Success(department.ParentDepartment);
        }

        if (parentId is null)
        {
            return Result<Department?>.Success(null);
        }

        var parent = await departments.FindAsync(parentId.Value, cancellationToken);
        if (parent is not { IsActive: true })
        {
            return DepartmentErrors.ParentNotFound;
        }

        if (department is not null && await IsAncestorOrSelfAsync(department.Id, parent.Id, cancellationToken))
        {
            return DepartmentErrors.ParentCycle;
        }

        return Result<Department?>.Success(parent);
    }

    /// <param name="currentHead">The current head when updating; null when creating.</param>
    public async Task<Result<Employee?>> ResolveHeadAsync(
        Guid? headId, Employee? currentHead, CancellationToken cancellationToken)
    {
        if (headId is null || headId == currentHead?.PublicId)
        {
            return Result<Employee?>.Success(headId is null ? null : currentHead);
        }

        // Resolved through the caller's data scope, like every other employee lookup (FR-8.3).
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);
        var head = await employees.FindAsync(headId.Value, scope, cancellationToken);
        return head is { IsActive: true } ? Result<Employee?>.Success(head) : DepartmentErrors.HeadNotFound;
    }

    /// <summary>True when <paramref name="departmentId"/> is <paramref name="candidateParentId"/> or one of its ancestors.</summary>
    private async Task<bool> IsAncestorOrSelfAsync(long departmentId, long candidateParentId, CancellationToken cancellationToken)
    {
        var parents = await departments.GetParentLinksAsync(cancellationToken);
        var visited = new HashSet<long>();
        for (long? current = candidateParentId; current is { } id && visited.Add(id); current = parents.GetValueOrDefault(id))
        {
            if (id == departmentId)
            {
                return true;
            }
        }

        return false;
    }
}
