using Attendance.Application.Scheduling.Abstractions;
using Attendance.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

internal sealed class WeeklyOffPatternRepository(AttendanceDbContext db) : IWeeklyOffPatternRepository
{
    public Task<WeeklyOffPattern?> FindAsync(Guid publicId, CancellationToken cancellationToken) =>
        db.WeeklyOffPatterns.Include(p => p.Days).SingleOrDefaultAsync(p => p.PublicId == publicId, cancellationToken);

    public async Task<IReadOnlyList<WeeklyOffPattern>> ListAsync(bool? isActive, CancellationToken cancellationToken) =>
        await db.WeeklyOffPatterns.AsNoTracking()
            .Include(p => p.Days)
            .Where(p => isActive == null || p.IsActive == isActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        db.WeeklyOffPatterns.AnyAsync(p => p.Name == name, cancellationToken);

    public void Add(WeeklyOffPattern pattern) => db.WeeklyOffPatterns.Add(pattern);
}

internal sealed class HolidayRepository(AttendanceDbContext db) : IHolidayRepository
{
    // Ignoring the soft-delete filter loads removed location links too, which SetLocations needs to
    // restore one rather than insert a duplicate key. The holiday itself must still be live.
    public Task<Holiday?> FindAsync(Guid publicId, CancellationToken cancellationToken) =>
        db.Holidays
            .IgnoreQueryFilters([ModelBuilderConventions.SoftDeleteFilter])
            .Include(h => h.Locations).ThenInclude(l => l.WorkLocation)
            .SingleOrDefaultAsync(h => h.PublicId == publicId && !h.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Holiday>> ListAsync(int year, long? workLocationId, CancellationToken cancellationToken)
    {
        // The soft-delete filter applies inside Include and Any, so only live links are loaded and tested.
        var holidays = db.Holidays.AsNoTracking()
            .Include(h => h.Locations).ThenInclude(l => l.WorkLocation)
            .Where(h => h.Year == year);

        if (workLocationId is { } id)
        {
            holidays = holidays.Where(h => !h.Locations.Any() || h.Locations.Any(l => l.WorkLocationId == id));
        }

        return await holidays.OrderBy(h => h.HolidayDate).ThenBy(h => h.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(DateOnly holidayDate, string name, long? exceptId, CancellationToken cancellationToken) =>
        db.Holidays.AnyAsync(
            h => h.HolidayDate == holidayDate && h.Name == name && (exceptId == null || h.Id != exceptId),
            cancellationToken);

    public void Add(Holiday holiday) => db.Holidays.Add(holiday);
}

internal sealed class CalendarRepository(AttendanceDbContext db) : ICalendarRepository
{
    public async Task<CalendarData> LoadAsync(
        IReadOnlyCollection<long> employeeIds, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var ids = employeeIds.ToArray();

        var employees = await db.Employees
            .Where(e => EF.Parameter(ids).Contains(e.Id))
            .Select(e => new { e.Id, e.WorkLocationId, e.DefaultShiftId, e.WeeklyOffPatternId })
            .ToListAsync(cancellationToken);

        // Identity resolution gives every entry on the same shift one Shift instance.
        var rosters = await db.EmployeeShiftRosters.AsNoTrackingWithIdentityResolution()
            .Include(r => r.Shift)
            .Where(r => EF.Parameter(ids).Contains(r.EmployeeId) && r.RosterDate >= from && r.RosterDate <= to)
            .ToListAsync(cancellationToken);

        var shiftIds = employees.Where(e => e.DefaultShiftId.HasValue).Select(e => e.DefaultShiftId!.Value).Distinct().ToArray();
        var shifts = await db.Shifts.AsNoTracking()
            .Where(s => EF.Parameter(shiftIds).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var patternIds = employees.Where(e => e.WeeklyOffPatternId.HasValue).Select(e => e.WeeklyOffPatternId!.Value).Distinct().ToArray();
        var patterns = await db.WeeklyOffPatterns.AsNoTracking()
            .Include(p => p.Days)
            .Where(p => EF.Parameter(patternIds).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var holidays = await db.Holidays.AsNoTracking()
            .Include(h => h.Locations)
            .Where(h => h.HolidayDate >= from && h.HolidayDate <= to)
            .ToListAsync(cancellationToken);

        var profiles = employees
            .Select(e => new EmployeeCalendarProfile(
                e.Id,
                e.WorkLocationId,
                e.DefaultShiftId is { } shiftId ? shifts[shiftId] : null,
                e.WeeklyOffPatternId is { } patternId ? patterns[patternId] : null))
            .ToList();

        return new CalendarData(from, to, profiles, rosters, holidays);
    }
}
