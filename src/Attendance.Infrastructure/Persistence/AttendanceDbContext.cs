using Attendance.Domain.Identity;
using Attendance.Domain.Organization;
using Attendance.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence;

public class AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : DbContext(options)
{
    // Identity and access
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Organization
    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Designation> Designations => Set<Designation>();

    public DbSet<WorkLocation> WorkLocations => Set<WorkLocation>();

    public DbSet<Employee> Employees => Set<Employee>();

    // Shift, roster and holidays
    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<WeeklyOffPattern> WeeklyOffPatterns => Set<WeeklyOffPattern>();

    public DbSet<WeeklyOffPatternDay> WeeklyOffPatternDays => Set<WeeklyOffPatternDay>();

    public DbSet<EmployeeShiftRoster> EmployeeShiftRosters => Set<EmployeeShiftRoster>();

    public DbSet<Holiday> Holidays => Set<Holiday>();

    public DbSet<HolidayLocation> HolidayLocations => Set<HolidayLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendanceDbContext).Assembly);

        // Runs last so it covers every entity type, including ones without a configuration class.
        modelBuilder.ApplyCrossCuttingConventions();
    }
}
