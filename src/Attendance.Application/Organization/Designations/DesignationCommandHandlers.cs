using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;

namespace Attendance.Application.Organization.Designations;

internal sealed class CreateDesignationCommandHandler(IMasterDataRepository<Designation> designations, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDesignationCommand, Result<DesignationDto>>
{
    public async Task<Result<DesignationDto>> HandleAsync(CreateDesignationCommand command, CancellationToken cancellationToken)
    {
        var code = OrgCodes.Normalize(command.Code);
        if (await designations.CodeExistsAsync(code, cancellationToken))
        {
            return DesignationErrors.CodeTaken(code);
        }

        var designation = new Designation { Code = code, Name = command.Name.Trim(), Grade = OrgCodes.NullIfBlank(command.Grade) };
        designations.Add(designation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return designation.ToDto();
    }
}

internal sealed class UpdateDesignationCommandHandler(IMasterDataRepository<Designation> designations, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateDesignationCommand, Result<DesignationDto>>
{
    public async Task<Result<DesignationDto>> HandleAsync(UpdateDesignationCommand command, CancellationToken cancellationToken)
    {
        var designation = await designations.FindAsync(command.PublicId, cancellationToken);
        if (designation is null)
        {
            return DesignationErrors.NotFound;
        }

        var code = OrgCodes.Normalize(command.Code);
        if (code != designation.Code && await designations.CodeExistsAsync(code, cancellationToken))
        {
            return DesignationErrors.CodeTaken(code);
        }

        designation.Code = code;
        designation.Name = command.Name.Trim();
        designation.Grade = OrgCodes.NullIfBlank(command.Grade);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return designation.ToDto();
    }
}

internal sealed class SetDesignationActiveCommandHandler(
    IMasterDataRepository<Designation> designations,
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetDesignationActiveCommand, Result<DesignationDto>>
{
    public async Task<Result<DesignationDto>> HandleAsync(SetDesignationActiveCommand command, CancellationToken cancellationToken)
    {
        var designation = await designations.FindAsync(command.PublicId, cancellationToken);
        if (designation is null)
        {
            return DesignationErrors.NotFound;
        }

        if (designation.IsActive == command.IsActive)
        {
            return designation.ToDto();
        }

        if (!command.IsActive && await employees.AnyActiveWithDesignationAsync(designation.Id, cancellationToken))
        {
            return DesignationErrors.InUse;
        }

        designation.IsActive = command.IsActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return designation.ToDto();
    }
}
