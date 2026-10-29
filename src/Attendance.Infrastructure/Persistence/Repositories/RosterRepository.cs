using System.Globalization;
using System.Text.Json;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Security;
using Attendance.Application.Scheduling.Abstractions;
using Attendance.Application.Scheduling.Rosters;
using Attendance.Domain.Scheduling;
using Attendance.Infrastructure.Persistence.Interceptors;
using Attendance.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

internal sealed class RosterRepository(AttendanceDbContext db, ICurrentUser currentUser, TimeProvider clock) : IRosterRepository
{
    public Task<EmployeeShiftRoster?> FindAsync(Guid publicId, EmployeeScope scope, CancellationToken cancellationToken) =>
        db.EmployeeShiftRosters
            .Include(r => r.Employee)
            .Include(r => r.Shift)
            .InScope(scope)
            .SingleOrDefaultAsync(r => r.PublicId == publicId, cancellationToken);

    public Task<EmployeeShiftRoster?> FindForDateAsync(long employeeId, DateOnly rosterDate, CancellationToken cancellationToken) =>
        db.EmployeeShiftRosters
            .IgnoreQueryFilters([ModelBuilderConventions.SoftDeleteFilter])
            .Include(r => r.Employee)
            .Include(r => r.Shift)
            .SingleOrDefaultAsync(r => r.EmployeeId == employeeId && r.RosterDate == rosterDate, cancellationToken);

    public async Task<PagedResult<EmployeeShiftRoster>> ListAsync(
        ListRosterEntriesQuery query, EmployeeScope scope, CancellationToken cancellationToken)
    {
        var rosters = db.EmployeeShiftRosters.AsNoTracking()
            .InScope(scope)
            .Where(r => r.RosterDate >= query.FromDate && r.RosterDate <= query.ToDate);

        if (query.EmployeeId is { } employeeId)
        {
            rosters = rosters.Where(r => r.Employee.PublicId == employeeId);
        }

        if (query.DepartmentId is { } departmentId)
        {
            rosters = rosters.Where(r => r.Employee.Department.PublicId == departmentId);
        }

        var totalCount = await rosters.CountAsync(cancellationToken);
        var items = await rosters
            .Include(r => r.Employee)
            .Include(r => r.Shift)
            .OrderBy(r => r.RosterDate)
            .ThenBy(r => r.Employee.EmployeeCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeShiftRoster>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<RosterAssignmentOutcome> AssignToDepartmentAsync(
        long departmentId,
        RosterAssignment assignment,
        EmployeeScope scope,
        CancellationToken cancellationToken)
    {
        var employeeIds = await db.Employees
            .InScope(scope)
            .Where(e => e.DepartmentId == departmentId && e.IsActive && e.JoiningDate <= assignment.ToDate)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        return await AssignAsync(employeeIds, assignment, cancellationToken);
    }

    public async Task<RosterAssignmentOutcome> AssignToEmployeesAsync(
        IReadOnlyCollection<long> employeeIds,
        RosterAssignment assignment,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds.ToArray();
        var joined = await db.Employees
            .Where(e => EF.Parameter(ids).Contains(e.Id) && e.JoiningDate <= assignment.ToDate)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        return await AssignAsync(joined, assignment, cancellationToken);
    }

    public async Task<RosterAssignmentOutcome> RemoveAsync(
        IReadOnlyCollection<long> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return new RosterAssignmentOutcome(0, 0, 0, [], null, null);
        }

        var employeeIdsJson = JsonSerializer.Serialize(employeeIds);
        var now = clock.GetUtcNow().UtcDateTime;
        var actor = currentUser.UserId ?? AuditableEntityInterceptor.SystemActor;

        // One statement: a soft delete of every live entry in the range, reporting what it removed.
        var changes = await db.Database.SqlQuery<RosterChange>($"""
            SET NOCOUNT ON;

            DECLARE @changed TABLE (EmployeeId bigint NOT NULL, RosterDate date NOT NULL);

            UPDATE r
            SET IsDeleted = CAST(1 AS bit),
                UpdatedAt = {now},
                UpdatedBy = {actor}
            OUTPUT inserted.EmployeeId, inserted.RosterDate INTO @changed
            FROM EmployeeShiftRosters AS r
            JOIN OPENJSON({employeeIdsJson}) WITH (Id bigint '$') AS ids ON ids.Id = r.EmployeeId
            WHERE r.RosterDate >= {from} AND r.RosterDate <= {to} AND r.IsDeleted = CAST(0 AS bit);

            SELECT EmployeeId,
                   MIN(RosterDate) AS FirstDate,
                   MAX(RosterDate) AS LastDate,
                   COUNT(*) AS Changed,
                   0 AS Inserted
            FROM @changed
            GROUP BY EmployeeId
            """).ToListAsync(cancellationToken);

        return ToOutcome(employeeIds.Count, changes);
    }

    private async Task<RosterAssignmentOutcome> AssignAsync(
        IReadOnlyList<long> employeeIds,
        RosterAssignment assignment,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return new RosterAssignmentOutcome(0, 0, 0, [], null, null);
        }

        var dayCount = assignment.ToDate.DayNumber - assignment.FromDate.DayNumber + 1;
        var dates = Enumerable.Range(0, dayCount)
            .Select(offset => assignment.FromDate.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        // Each list travels as one JSON parameter, so neither size can reach SQL Server's 2,100-parameter limit.
        var employeeIdsJson = JsonSerializer.Serialize(employeeIds);
        var datesJson = JsonSerializer.Serialize(dates);

        // The change tracker isn't involved, so the audit columns are stamped here as the interceptor would.
        var now = clock.GetUtcNow().UtcDateTime;
        var actor = currentUser.UserId ?? AuditableEntityInterceptor.SystemActor;
        var shiftId = assignment.ShiftId;
        var isWeeklyOffOverride = assignment.IsWeeklyOffOverride;
        var remarks = assignment.Remarks;

        // Two set-based statements cover every employee-day: update the rows that exist but differ (or were
        // removed), then insert the missing ones. UPDLOCK + HOLDLOCK keep a concurrent assignment from
        // inserting between the two. EXCEPT compares NULLs as equal, which <> doesn't. NEWID() is a random
        // (v4) UUID, as the PublicId convention requires; it is never NEWSEQUENTIALID().
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var changes = await db.Database.SqlQuery<RosterChange>($"""
            SET NOCOUNT ON;

            DECLARE @changed TABLE (EmployeeId bigint NOT NULL, RosterDate date NOT NULL, IsInsert bit NOT NULL);

            WITH Planned AS (
                SELECT e.Id AS EmployeeId, d.RosterDate
                FROM OPENJSON({employeeIdsJson}) WITH (Id bigint '$') AS ids
                JOIN Employees AS e ON e.Id = ids.Id
                CROSS JOIN OPENJSON({datesJson}) WITH (RosterDate date '$') AS d
                WHERE d.RosterDate >= e.JoiningDate
            )
            UPDATE r
            SET ShiftId = {shiftId},
                IsWeeklyOffOverride = CAST({isWeeklyOffOverride} AS bit),
                Remarks = CAST({remarks} AS nvarchar(300)),
                IsDeleted = CAST(0 AS bit),
                UpdatedAt = {now},
                UpdatedBy = {actor}
            OUTPUT inserted.EmployeeId, inserted.RosterDate, CAST(0 AS bit) INTO @changed
            FROM EmployeeShiftRosters AS r WITH (UPDLOCK, HOLDLOCK)
            JOIN Planned AS p ON p.EmployeeId = r.EmployeeId AND p.RosterDate = r.RosterDate
            WHERE EXISTS (
                SELECT r.ShiftId, r.IsWeeklyOffOverride, r.Remarks, r.IsDeleted
                EXCEPT
                SELECT {shiftId}, CAST({isWeeklyOffOverride} AS bit), CAST({remarks} AS nvarchar(300)), CAST(0 AS bit));

            WITH Planned AS (
                SELECT e.Id AS EmployeeId, d.RosterDate
                FROM OPENJSON({employeeIdsJson}) WITH (Id bigint '$') AS ids
                JOIN Employees AS e ON e.Id = ids.Id
                CROSS JOIN OPENJSON({datesJson}) WITH (RosterDate date '$') AS d
                WHERE d.RosterDate >= e.JoiningDate
            )
            INSERT INTO EmployeeShiftRosters (PublicId, EmployeeId, RosterDate, ShiftId, IsWeeklyOffOverride, Remarks, IsDeleted, CreatedAt, CreatedBy)
            OUTPUT inserted.EmployeeId, inserted.RosterDate, CAST(1 AS bit) INTO @changed
            SELECT NEWID(), p.EmployeeId, p.RosterDate, {shiftId}, CAST({isWeeklyOffOverride} AS bit), CAST({remarks} AS nvarchar(300)),
                   CAST(0 AS bit), {now}, {actor}
            FROM Planned AS p
            WHERE NOT EXISTS (
                SELECT 1 FROM EmployeeShiftRosters AS r WITH (UPDLOCK, HOLDLOCK)
                WHERE r.EmployeeId = p.EmployeeId AND r.RosterDate = p.RosterDate);

            SELECT EmployeeId,
                   MIN(RosterDate) AS FirstDate,
                   MAX(RosterDate) AS LastDate,
                   COUNT(*) AS Changed,
                   SUM(CAST(IsInsert AS int)) AS Inserted
            FROM @changed
            GROUP BY EmployeeId
            """).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToOutcome(employeeIds.Count, changes);
    }

    private static RosterAssignmentOutcome ToOutcome(int employeeCount, List<RosterChange> changes) => new(
        employeeCount,
        Created: changes.Sum(c => c.Inserted),
        Updated: changes.Sum(c => c.Changed - c.Inserted),
        [.. changes.Select(c => c.EmployeeId)],
        changes.Count == 0 ? null : changes.Min(c => c.FirstDate),
        changes.Count == 0 ? null : changes.Max(c => c.LastDate));

    public void Add(EmployeeShiftRoster roster) => db.EmployeeShiftRosters.Add(roster);

    /// <summary>The entries one employee gained, had changed or lost in a set-based assignment or removal.</summary>
    private sealed class RosterChange
    {
        public long EmployeeId { get; init; }

        public DateOnly FirstDate { get; init; }

        public DateOnly LastDate { get; init; }

        public int Changed { get; init; }

        public int Inserted { get; init; }
    }
}
