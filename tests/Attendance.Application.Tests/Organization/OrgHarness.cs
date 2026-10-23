using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Security;
using Attendance.Application.Tests.Persistence;
using Attendance.Domain.Organization;
using Attendance.Infrastructure.Persistence.Repositories;

namespace Attendance.Application.Tests.Organization;

/// <summary>
/// A SQLite database with one active department, designation and work location, plus the real
/// repositories over it. The caller's scope is set directly through <see cref="Caller"/>.
/// </summary>
internal sealed class OrgHarness : IDisposable
{
    private readonly SqliteTestDatabase _db = new();

    public FakeUserContext Caller { get; } = new();

    public Department Department { get; private set; } = null!;

    public Designation Designation { get; private set; } = null!;

    public WorkLocation WorkLocation { get; private set; } = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<OrgHarness> CreateAsync()
    {
        var harness = new OrgHarness();
        await using var context = harness.CreateContext();
        harness.Department = new Department { Code = "SEW", Name = "Sewing" };
        harness.Designation = new Designation { Code = "OPR", Name = "Sewing Operator" };
        harness.WorkLocation = new WorkLocation { Code = "DHK", Name = "Dhaka Unit" };
        context.AddRange(harness.Department, harness.Designation, harness.WorkLocation);
        await context.SaveChangesAsync(Ct);
        return harness;
    }

    public TestDbContext CreateContext() => _db.CreateContext();

    public async Task<Employee> AddEmployeeAsync(string code, Employee? manager = null, bool isActive = true)
    {
        await using var context = CreateContext();
        var employee = new Employee
        {
            EmployeeCode = code,
            FirstName = "Test",
            LastName = code,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Female,
            JoiningDate = new DateOnly(2020, 1, 1),
            EmploymentType = EmploymentType.Permanent,
            IsActive = isActive,
            DepartmentId = Department.Id,
            DesignationId = Designation.Id,
            WorkLocationId = WorkLocation.Id,
            ReportingManagerId = manager?.Id,
        };
        context.Employees.Add(employee);
        await context.SaveChangesAsync(Ct);
        return employee;
    }

    public async Task RetireDepartmentAsync()
    {
        await using var context = CreateContext();
        (await context.Departments.FindAsync([Department.Id], Ct))!.IsActive = false;
        await context.SaveChangesAsync(Ct);
    }

    public EmployeeScopeResolver Scopes() => new(Caller);

    public static EmployeeRepository Employees(TestDbContext context) => new(context);

    public static DepartmentRepository Departments(TestDbContext context) => new(context);

    public static MasterDataRepository<T> MasterData<T>(TestDbContext context)
        where T : Domain.Common.BaseEntity, Domain.Common.IMasterData => new(context);

    public void Dispose() => _db.Dispose();
}

/// <summary>A caller with company-wide scope unless <see cref="RestrictTo"/> is called.</summary>
internal sealed class FakeUserContext : IUserContextService
{
    private HashSet<string> _permissions = [Domain.Identity.Permissions.Scope.Company];
    private long? _employeeId;
    private IReadOnlySet<long> _tree = new HashSet<long>();

    /// <summary>Takes away company scope: the caller sees <paramref name="self"/> and <paramref name="tree"/> only.</summary>
    public void RestrictTo(long? self, params long[] tree)
    {
        _permissions = [];
        _employeeId = self;
        _tree = tree.ToHashSet();
    }

    public Task<UserContext> GetCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new UserContext(1, Guid.NewGuid(), _employeeId, _permissions));

    public Task<IReadOnlySet<long>> GetReportingTreeAsync(CancellationToken cancellationToken) => Task.FromResult(_tree);
}
