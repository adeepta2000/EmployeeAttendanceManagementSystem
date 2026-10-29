using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization.Abstractions;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Domain.Scheduling;
using Attendance.Shared.Results;

namespace Attendance.Application.Scheduling.Holidays;

internal sealed class CreateHolidayCommandHandler(
    IHolidayRepository holidays,
    HolidayLocationResolver locations,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateHolidayCommand, Result<HolidayDto>>
{
    public async Task<Result<HolidayDto>> HandleAsync(CreateHolidayCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await holidays.ExistsAsync(command.HolidayDate, name, exceptId: null, cancellationToken))
        {
            return HolidayErrors.Duplicate(command.HolidayDate, name);
        }

        var workLocations = await locations.ResolveAsync(command.WorkLocationIds, holiday: null, cancellationToken);
        if (!workLocations.IsSuccess)
        {
            return workLocations.Error;
        }

        var holiday = new Holiday { Name = name, HolidayDate = command.HolidayDate };
        holiday.Apply(command);
        holiday.SetLocations(workLocations.Value.Select(l => l.Id));
        holidays.Add(holiday);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return holiday.ToDto();
    }
}

internal sealed class UpdateHolidayCommandHandler(
    IHolidayRepository holidays,
    HolidayLocationResolver locations,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateHolidayCommand, Result<HolidayDto>>
{
    public async Task<Result<HolidayDto>> HandleAsync(UpdateHolidayCommand command, CancellationToken cancellationToken)
    {
        var holiday = await holidays.FindAsync(command.PublicId, cancellationToken);
        if (holiday is null)
        {
            return HolidayErrors.NotFound;
        }

        var name = command.Name.Trim();
        if (await holidays.ExistsAsync(command.HolidayDate, name, holiday.Id, cancellationToken))
        {
            return HolidayErrors.Duplicate(command.HolidayDate, name);
        }

        var workLocations = await locations.ResolveAsync(command.WorkLocationIds, holiday, cancellationToken);
        if (!workLocations.IsSuccess)
        {
            return workLocations.Error;
        }

        holiday.Apply(command);
        holiday.SetLocations(workLocations.Value.Select(l => l.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return holiday.ToDto();
    }
}

internal sealed class DeleteHolidayCommandHandler(IHolidayRepository holidays, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteHolidayCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteHolidayCommand command, CancellationToken cancellationToken)
    {
        var holiday = await holidays.FindAsync(command.PublicId, cancellationToken);
        if (holiday is null)
        {
            return Result.Failure(HolidayErrors.NotFound);
        }

        holiday.Remove();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Resolves the work locations a create or update limits a holiday to.</summary>
internal sealed class HolidayLocationResolver(IMasterDataRepository<WorkLocation> workLocations)
{
    /// <param name="holiday">The holiday being updated, with its links loaded; null when creating one.</param>
    public async Task<Result<IReadOnlyList<WorkLocation>>> ResolveAsync(
        IReadOnlyList<Guid>? publicIds,
        Holiday? holiday,
        CancellationToken cancellationToken)
    {
        if (publicIds is not { Count: > 0 })
        {
            return Result<IReadOnlyList<WorkLocation>>.Success([]);
        }

        var found = await workLocations.FindManyAsync(publicIds, cancellationToken);
        var linked = holiday?.WorkLocationIds.ToHashSet() ?? [];

        // A retired location stays acceptable while the holiday is already limited to it.
        if (found.Count != publicIds.Count || found.Any(l => !l.IsActive && !linked.Contains(l.Id)))
        {
            return HolidayErrors.WorkLocationNotFound;
        }

        return Result<IReadOnlyList<WorkLocation>>.Success(found);
    }
}
