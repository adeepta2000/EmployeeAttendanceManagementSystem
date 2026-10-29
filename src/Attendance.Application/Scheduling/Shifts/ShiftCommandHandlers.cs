using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;

namespace Attendance.Application.Scheduling.Shifts;

internal sealed class CreateShiftCommandHandler(IMasterDataRepository<Shift> shifts, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateShiftCommand, Result<ShiftDto>>
{
    public async Task<Result<ShiftDto>> HandleAsync(CreateShiftCommand command, CancellationToken cancellationToken)
    {
        var code = OrgCodes.Normalize(command.Code);
        if (await shifts.CodeExistsAsync(code, cancellationToken))
        {
            return ShiftErrors.CodeTaken(code);
        }

        var shift = ShiftRules.CreateShift(command);
        shifts.Add(shift);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return shift.ToDto();
    }
}

internal sealed class UpdateShiftCommandHandler(IMasterDataRepository<Shift> shifts, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateShiftCommand, Result<ShiftDto>>
{
    public async Task<Result<ShiftDto>> HandleAsync(UpdateShiftCommand command, CancellationToken cancellationToken)
    {
        var shift = await shifts.FindAsync(command.PublicId, cancellationToken);
        if (shift is null)
        {
            return ShiftErrors.NotFound;
        }

        var code = OrgCodes.Normalize(command.Code);
        if (code != shift.Code && await shifts.CodeExistsAsync(code, cancellationToken))
        {
            return ShiftErrors.CodeTaken(code);
        }

        shift.Apply(command);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return shift.ToDto();
    }
}

internal sealed class SetShiftActiveCommandHandler(
    IMasterDataRepository<Shift> shifts,
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetShiftActiveCommand, Result<ShiftDto>>
{
    public async Task<Result<ShiftDto>> HandleAsync(SetShiftActiveCommand command, CancellationToken cancellationToken)
    {
        var shift = await shifts.FindAsync(command.PublicId, cancellationToken);
        if (shift is null)
        {
            return ShiftErrors.NotFound;
        }

        if (shift.IsActive == command.IsActive)
        {
            return shift.ToDto();
        }

        // Roster entries may keep pointing at a retired shift: they are history, or a plan made before it retired.
        if (!command.IsActive && await employees.AnyActiveWithDefaultShiftAsync(shift.Id, cancellationToken))
        {
            return ShiftErrors.InUse;
        }

        shift.IsActive = command.IsActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return shift.ToDto();
    }
}
