using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Common;
using Attendance.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

internal class MasterDataRepository<T>(AttendanceDbContext db) : IMasterDataRepository<T>
    where T : BaseEntity, IMasterData
{
    protected AttendanceDbContext Db => db;

    /// <summary>The query that finds and lists start from; override it to load navigations.</summary>
    protected virtual IQueryable<T> Query => db.Set<T>();

    public Task<T?> FindAsync(Guid publicId, CancellationToken cancellationToken) =>
        Query.SingleOrDefaultAsync(e => e.PublicId == publicId, cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(bool? isActive, CancellationToken cancellationToken) =>
        await Query.AsNoTracking()
            .Where(e => isActive == null || e.IsActive == isActive)
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Set<T>().AnyAsync(e => e.Code == code, cancellationToken);

    public async Task<IReadOnlyDictionary<string, long>> GetActiveIdsByCodeAsync(CancellationToken cancellationToken) =>
        await db.Set<T>()
            .Where(e => e.IsActive)
            .ToDictionaryAsync(e => e.Code, e => e.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

    public void Add(T entity) => db.Set<T>().Add(entity);
}

internal sealed class DepartmentRepository(AttendanceDbContext db) : MasterDataRepository<Department>(db), IDepartmentRepository
{
    protected override IQueryable<Department> Query =>
        Db.Departments.Include(d => d.ParentDepartment).Include(d => d.HeadEmployee);

    public async Task<IReadOnlyDictionary<long, long?>> GetParentLinksAsync(CancellationToken cancellationToken) =>
        await Db.Departments.ToDictionaryAsync(d => d.Id, d => d.ParentDepartmentId, cancellationToken);

    public Task<bool> HasActiveChildrenAsync(long departmentId, CancellationToken cancellationToken) =>
        Db.Departments.AnyAsync(d => d.ParentDepartmentId == departmentId && d.IsActive, cancellationToken);
}
