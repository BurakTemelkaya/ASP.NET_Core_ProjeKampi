using CoreLayer.DataAccess.EntityFramework;
using DataAccessLayer.Abstract;
using EntityLayer.Concrete;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Concrete.EntityFramework;

public class EfMessageDraftRepository : EfEntityRepositoryBase<MessageDraft>, IMessageDraftDal
{

    public EfMessageDraftRepository(Context context) : base(context)
    {
        Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    private Context Context => _context as Context;

    private CancellationToken CancellationToken =>
        base.HttpContextAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None;

    public async Task<List<MessageDraft>> GetMessageDraftListAsync(Expression<Func<MessageDraft, bool>> filter = null)
    {
        return filter == null ?
        await Context.MessagesDrafts.Include(x => x.User).ToListAsync(CancellationToken) :
        await Context.MessagesDrafts.Include(x => x.User).Where(filter).ToListAsync(CancellationToken);
    }
    public async Task<List<MessageDraft>> GetMessageDraftListByUserIdAsync(int id, Expression<Func<MessageDraft, bool>> filter = null)
    {
        return filter == null ?
            await Context.MessagesDrafts.Include(x => x.User).Where(x => x.UserId == id).ToListAsync(CancellationToken) :
            await Context.MessagesDrafts.Include(x => x.User).Where(x => x.UserId == id).Where(filter).ToListAsync(CancellationToken);
    }
    public async Task<MessageDraft> GetMessageDraftByUserIdAsync(int id, Expression<Func<MessageDraft, bool>> filter = null)
    {
        return filter == null ?
            await Context.MessagesDrafts.Include(x => x.User).Where(x => x.UserId == id).FirstOrDefaultAsync(CancellationToken) :
            await Context.MessagesDrafts.Include(x => x.User).Where(x => x.UserId == id).Where(filter).FirstOrDefaultAsync(CancellationToken);
    }
}
