using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;

namespace Attendance.Application.Organization.WorkLocations;

internal sealed class CreateWorkLocationCommandHandler(IMasterDataRepository<WorkLocation> workLocations, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWorkLocationCommand, Result<WorkLocationDto>>
{
    public async Task<Result<WorkLocationDto>> HandleAsync(CreateWorkLocationCommand command, CancellationToken cancellationToken)
    {
        var code = OrgCodes.Normalize(command.Code);
        if (await workLocations.CodeExistsAsync(code, cancellationToken))
        {
            return WorkLocationErrors.CodeTaken(code);
        }

        var location = new WorkLocation { Code = code, Name = command.Name };
        location.Apply(command);
        workLocations.Add(location);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return location.ToDto();
    }
}

internal sealed class UpdateWorkLocationCommandHandler(IMasterDataRepository<WorkLocation> workLocations, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkLocationCommand, Result<WorkLocationDto>>
{
    public async Task<Result<WorkLocationDto>> HandleAsync(UpdateWorkLocationCommand command, CancellationToken cancellationToken)
    {
        var location = await workLocations.FindAsync(command.PublicId, cancellationToken);
        if (location is null)
        {
            return WorkLocationErrors.NotFound;
        }

        var code = OrgCodes.Normalize(command.Code);
        if (code != location.Code && await workLocations.CodeExistsAsync(code, cancellationToken))
        {
            return WorkLocationErrors.CodeTaken(code);
        }

        location.Apply(command);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return location.ToDto();
    }
}

internal sealed class SetWorkLocationActiveCommandHandler(
    IMasterDataRepository<WorkLocation> workLocations,
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetWorkLocationActiveCommand, Result<WorkLocationDto>>
{
    public async Task<Result<WorkLocationDto>> HandleAsync(SetWorkLocationActiveCommand command, CancellationToken cancellationToken)
    {
        var location = await workLocations.FindAsync(command.PublicId, cancellationToken);
        if (location is null)
        {
            return WorkLocationErrors.NotFound;
        }

        if (location.IsActive == command.IsActive)
        {
            return location.ToDto();
        }

        if (!command.IsActive && await employees.AnyActiveAtWorkLocationAsync(location.Id, cancellationToken))
        {
            return WorkLocationErrors.InUse;
        }

        location.IsActive = command.IsActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return location.ToDto();
    }
}
