using System.Net;
using Attendance.Application.Scheduling;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>Sets up shifts, weekly-off patterns and schedules through the API, for tests about something else.</summary>
internal static class SchedulingApi
{
    /// <summary>A shift with a code no other test uses. Times are "HH:mm:ss".</summary>
    public static async Task<ShiftDto> CreateShiftAsync(this ApiSession hrManager, string start = "09:00:00", string end = "18:00:00") =>
        await (await hrManager.PostAsync("/api/shifts", ShiftBody(AttendanceApiFactory.UniqueCode("S"), start, end)))
            .ReadAsync<ShiftDto>(HttpStatusCode.Created);

    /// <summary>A valid shift request body: a one-hour break, 7 hours for a full day, 4 for a half day.</summary>
    public static object ShiftBody(string code, string start, string end, bool? crossesMidnight = null) => new
    {
        code,
        name = $"Shift {code}",
        startTime = start,
        endTime = end,
        crossesMidnight,
        breakMinutes = 60,
        graceInMinutes = 10,
        graceOutMinutes = 5,
        halfDayThresholdMinutes = 240,
        fullDayMinutes = 420,
        punchWindowBeforeMinutes = 120,
        punchWindowAfterMinutes = 240,
    };

    /// <summary>Every Friday plus the 2nd and 4th Saturday, under a name no other test uses.</summary>
    public static async Task<WeeklyOffPatternDto> CreateFridayAndAlternateSaturdaysAsync(this ApiSession hrManager) =>
        await (await hrManager.PostAsync("/api/weekly-off-patterns", new
        {
            name = $"Friday + alternate Saturdays {Guid.NewGuid():N}",
            days = new object[]
            {
                new { dayOfWeek = "Friday" },
                new { dayOfWeek = "Saturday", weekOfMonth = 2 },
                new { dayOfWeek = "Saturday", weekOfMonth = 4 },
            },
        })).ReadAsync<WeeklyOffPatternDto>(HttpStatusCode.Created);

    public static async Task<EmployeeScheduleDto> SetScheduleAsync(
        this ApiSession session, TestEmployee employee, Guid? defaultShiftId, Guid? weeklyOffPatternId = null) =>
        await (await session.PutAsync($"/api/employees/{employee.PublicId}/schedule", new { defaultShiftId, weeklyOffPatternId }))
            .ReadAsync<EmployeeScheduleDto>();
}
