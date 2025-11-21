using CoreLayer.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using X.PagedList;
using X.PagedList.EF;

namespace CoreLayer.DataAccess.EntityFramework;

public class EfEntityRepositoryBase<TEntity> : IEntityRepository<TEntity>
    where TEntity : class, IEntity, new()
{

    protected readonly DbContext _context;
    private IHttpContextAccessor _httpContextAccessor;
    

    public EfEntityRepositoryBase(DbContext context)
    {
        _context = context;
    }

    protected IHttpContextAccessor HttpContextAccessor =>
    _httpContextAccessor ??= _context.Database.GetService<IHttpContextAccessor>();


    private CancellationToken CancellationToken =>
        HttpContextAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None;

    public async Task DeleteAsync(TEntity t)
    {
        _context.Remove(t);
        await _context.SaveChangesAsync(CancellationToken);
    }

    public async Task DeleteRangeAsync(List<TEntity> t)
    {
        _context.RemoveRange(t);
        await _context.SaveChangesAsync(CancellationToken);
    }

    public async Task<TEntity> GetByIDAsync(int id, bool enableTracking = false)
    {
        TEntity result = await _context.Set<TEntity>().FindAsync(id, CancellationToken);

        if (result != null && !enableTracking)
        {
            _context.Entry(result).State = EntityState.Detached;
        }

        return result;
    }

    public async Task<List<TEntity>> GetListAllAsync(Expression<Func<TEntity, bool>> filter = null, int take = 0, int skip = 0
        , bool sortInReverse = true, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include = null, bool enableTracking = false)
    {
        var queryable = _context.Set<TEntity>().AsQueryable();

        if (take > 0)
        {
            if (sortInReverse)
            {
                var count = await GetCountAsync(filter);
                if (skip == 0)
                {
                    skip = count - take;
                }
                else if (skip > 0)
                {
                    skip = count - skip;
                }

                if (skip < 0)
                {
                    skip = 0;
                }
            }

            if (include != null) queryable = include(queryable);

            if (!enableTracking && include == null) queryable = queryable.AsNoTracking();

            if (!enableTracking && include != null) queryable = queryable.AsNoTrackingWithIdentityResolution();

            var filteredResult = filter == null ?
            await queryable.Skip(skip).Take(take).ToListAsync(CancellationToken) :
            await queryable.Where(filter).Skip(skip).Take(take).ToListAsync(CancellationToken);

            return filteredResult;
        }

        if (include != null) queryable = include(queryable);

        var result = filter == null ?
            await queryable.ToListAsync(CancellationToken) :
            await queryable.Where(filter).ToListAsync(CancellationToken);

        if (sortInReverse)
        {
            result.Reverse();
        }

        return result;
    }

    [ObsoleteAttribute(message:"Yerini X.Paged.List kütüphanesi metodları aldı.")]
    public async Task<List<TEntity>> GetListAllByPagingAsync(Expression<Func<TEntity, bool>> filter = null, int take = 0, int page = 1
        , bool sortInReverse = true, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include = null, bool enableTracking = false)
    {
        List<TEntity> values = new List<TEntity>();
        int count = await GetCountAsync(filter);
        int skip = count - (take * page);

        if (skip >= count)
        {
            skip = 0;
            page = 1;
        }

        values.AddRange(AddNullObject<TEntity>.GetNullValuesForBefore(page, take));

        int realTake = 0;

        if (skip < 0)
        {
            realTake += take + skip;
            skip = 0;
        }
        else
        {
            realTake = take;
        }

        IQueryable<TEntity> queryable = _context.Set<TEntity>().AsQueryable();

        if (include != null) queryable = include(queryable);

        if (!enableTracking) queryable = queryable.AsNoTracking();

        List<TEntity> result = filter == null ?
            await queryable.Skip(skip).Take(realTake).ToListAsync(CancellationToken) :
            await queryable.Where(filter).Skip(skip).Take(realTake).ToListAsync(CancellationToken);

        if (sortInReverse)
        {
            result.Reverse();
        }

        values.AddRange(result);

        values.AddRange(AddNullObject<TEntity>.GetNullValuesForAfter(page, take, count));

        return values;
    }

    public async Task<TEntity> InsertAsync(TEntity t)
    {
        await _context.AddAsync(t, CancellationToken);
        await _context.SaveChangesAsync(CancellationToken);

        return t;
    }

    public async Task<List<TEntity>> InsertRangeAsync(List<TEntity> t)
    {
        await _context.AddRangeAsync(t, CancellationToken);
        await _context.SaveChangesAsync(CancellationToken);

        return t;
    }

    public async Task<TEntity> GetByFilterAsync(Expression<Func<TEntity, bool>> filter = null, bool enableTracking = false)
    {
        IQueryable<TEntity> queryable = _context.Set<TEntity>().AsQueryable();

        if (filter != null) queryable = queryable.Where(filter);

        if (!enableTracking) queryable = queryable.AsNoTracking();

        return await queryable.FirstOrDefaultAsync(CancellationToken);
    }

    public async Task<TEntity> UpdateAsync(TEntity t)
    {
        _context.Update(t);
        await _context.SaveChangesAsync(CancellationToken);

        return t;
    }

    public async Task<List<TEntity>> UpdateRangeAsync(List<TEntity> t)
    {
        _context.UpdateRange(t);
        await _context.SaveChangesAsync(CancellationToken);

        return t;
    }

    public async Task<int> GetCountAsync(Expression<Func<TEntity, bool>> filter = null, bool enableTracking = false)
    {
        var queryable = _context.Set<TEntity>().AsQueryable();

        if (filter != null) queryable = queryable.Where(filter);

        if (!enableTracking) queryable = queryable.AsNoTracking();

        return await queryable.CountAsync(CancellationToken);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync(CancellationToken);
    }

    public async Task<Dictionary<DateTime, int>> GetChartDataAsync(
    string dateSelector,
    TimeUnit timeUnit, // Enum parametre
    DateTime? startDate = null,
    DateTime? endDate = null,
    Expression<Func<TEntity, bool>> predicate = null)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        if (startDate.HasValue)
        {
            query = query.Where(e => EF.Property<DateTime>(e, dateSelector) >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => EF.Property<DateTime>(e, dateSelector) <= endDate.Value);
        }

        IQueryable<IGrouping<DateTime, TEntity>> groupedQuery;

        switch (timeUnit)
        {
            case TimeUnit.Hour:
                groupedQuery = query.GroupBy(e => new DateTime(
                    EF.Property<DateTime>(e, dateSelector).Year,
                    EF.Property<DateTime>(e, dateSelector).Month,
                    EF.Property<DateTime>(e, dateSelector).Day,
                    EF.Property<DateTime>(e, dateSelector).Hour, 0, 0));
                break;

            case TimeUnit.Day:
                groupedQuery = query.GroupBy(e => EF.Property<DateTime>(e, dateSelector).Date);
                break;

            default:
                throw new ArgumentException("Invalid time unit.");
        }

        // Veritabanında gruplama ve sayma işlemi
        var groupedData = await groupedQuery
            .Select(g => new
            {
                DateTime = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(g => g.DateTime, g => g.Count, CancellationToken);

        return groupedData;
    }


    public async Task<IPagedList<TEntity>> GetPagedListAsync(
    int pageNumber,
    int pageSize,
    Expression<Func<TEntity, bool>> predicate = null,
    Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include = null,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
    bool enableTracking = false)
    {
        IQueryable<TEntity> query = _context.Set<TEntity>();

        if (!enableTracking && include == null)
        {
            query = query.AsNoTracking();
        }

        if (include != null)
        {
            query = include(query);

            if (!enableTracking)
            {
                query = query.AsNoTrackingWithIdentityResolution();
            }
        }

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        return await query.ToPagedListAsync(pageNumber, pageSize, null);
    }
}