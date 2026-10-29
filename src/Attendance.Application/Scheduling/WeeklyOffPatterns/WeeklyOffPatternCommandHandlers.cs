using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;

namespace Attendance.Application.Scheduling.WeeklyOffPatterns;

internal sealed class CreateWeeklyOffPatternCommandHandler(IWeeklyOffPatternRepository patterns, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWeeklyOffPatternCommand, Result<WeeklyOffPatternDto>>
{
    public async Task<Result<WeeklyOffPatternDto>> HandleAsync(CreateWeeklyOffPatternCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await patterns.NameExistsAsync(name, cancellationToken))
        {
            return WeeklyOffPatternErrors.NameTaken(name);
        }

        var pattern = new WeeklyOffPattern(name, command.Days);
        patterns.Add(pattern);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return pattern.ToDto();
    }
}

internal sealed class RenameWeeklyOffPatternCommandHandler(IWeeklyOffPatternRepository patterns, IUnitOfWork unitOfWork)
    : ICommandHandler<RenameWeeklyOffPatternCommand, Result<WeeklyOffPatternDto>>
{
    public async Task<Result<WeeklyOffPatternDto>> HandleAsync(RenameWeeklyOffPatternCommand command, CancellationToken cancellationToken)
    {
        var pattern = await patterns.FindAsync(command.PublicId, cancellationToken);
        if (pattern is null)
        {
            return WeeklyOffPatternErrors.NotFound;
        }

        // A change of case only is allowed: the name check uses the database's case-insensitive collation.
        var name = command.Name.Trim();
        if (!string.Equals(name, pattern.Name, StringComparison.OrdinalIgnoreCase)
            && await patterns.NameExistsAsync(name, cancellationToken))
        {
            return WeeklyOffPatternErrors.NameTaken(name);
        }

        pattern.Name = name;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return pattern.ToDto();
    }
}

internal sealed class SetWeeklyOffPatternActiveCommandHandler(
    IWeeklyOffPatternRepository patterns,
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetWeeklyOffPatternActiveCommand, Result<WeeklyOffPatternDto>>
{
    public async Task<Result<WeeklyOffPatternDto>> HandleAsync(SetWeeklyOffPatternActiveCommand command, CancellationToken cancellationToken)
    {
        var pattern = await patterns.FindAsync(command.PublicId, cancellationToken);
        if (pattern is null)
        {
            return WeeklyOffPatternErrors.NotFound;
        }

        if (pattern.IsActive == command.IsActive)
        {
            return pattern.ToDto();
        }

        if (!command.IsActive && await employees.AnyActiveWithWeeklyOffPatternAsync(pattern.Id, cancellationToken))
        {
            return WeeklyOffPatternErrors.InUse;
        }

        pattern.IsActive = command.IsActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return pattern.ToDto();
    }
}
