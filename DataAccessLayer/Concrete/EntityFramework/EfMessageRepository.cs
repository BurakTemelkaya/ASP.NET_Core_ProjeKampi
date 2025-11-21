using CoreLayer.DataAccess.EntityFramework;
using DataAccessLayer.Abstract;
using EntityLayer.Concrete;
using EntityLayer.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Concrete.EntityFramework;

public class EfMessageRepository : EfEntityRepositoryBase<Message>, IMessageDal
{

    public EfMessageRepository(Context context) : base(context)
    {

    }

    private Context Context => _context as Context;

    private CancellationToken CancellationToken =>
        base.HttpContextAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None;

    public async Task<List<MessageSenderUserDto>> GetInboxWithMessageListAsync(int id, Expression<Func<MessageSenderUserDto, bool>> filter = null, int take = 0, int skip = 0)
    {
        var query = Context.Messages
           .Include(x => x.SenderUser)
               .Where(x => x.ReceiverUserId == id)
               .Select(x => new MessageSenderUserDto
               {
                   MessageID = x.MessageID,
                   Subject = x.Subject,
                   Details = x.Details,
                   MessageDate = x.MessageDate,
                   MessageStatus = x.MessageStatus,
                   SenderUserName = x.SenderUser.UserName,
                   SenderNameSurname = x.SenderUser.NameSurname,
                   SenderImageUrl = x.SenderUser.ImageUrl,
                   SenderUserId = x.SenderUserId,
                   ReceiverUserId = x.ReceiverUserId
               });

        query = filter == null ?
           query : query.Where(filter);

        query = query.OrderByDescending(x => x.MessageID).AsQueryable();

        query = take > 0 ? query.Skip(skip).Take(take) : query;

        return await query.AsNoTrackingWithIdentityResolution().ToListAsync(CancellationToken);
    }
    public async Task<Message> GetReceivedMessageAsync(int id, Expression<Func<Message, bool>> filter = null)
    {
        return filter == null ?
            await Context.Messages.Include(x => x.SenderUser)
        .Where(x => x.ReceiverUser.Id == id).FirstOrDefaultAsync() :
        await Context.Messages.Include(x => x.SenderUser)
        .Where(x => x.ReceiverUser.Id == id).FirstOrDefaultAsync(filter, CancellationToken);
    }
    public async Task<Message> GetSendedMessageAsync(int id, Expression<Func<Message, bool>> filter = null)
    {
        return filter == null ?
           await Context.Messages.Include(x => x.ReceiverUser)
        .Where(x => x.SenderUser.Id == id).AsNoTracking().FirstOrDefaultAsync() :
        await Context.Messages.Include(x => x.ReceiverUser)
        .Where(x => x.SenderUser.Id == id).AsNoTracking().FirstOrDefaultAsync(filter, CancellationToken);
    }

    public async Task<List<MessageReceiverUserDto>> GetSendBoxWithMessageListAsync(int id, Expression<Func<MessageReceiverUserDto, bool>> filter = null, int take = 0, int skip = 0)
    {
        var query = Context.Messages
           .Include(x => x.ReceiverUser)
               .Where(x => x.SenderUserId == id)
               .Select(x => new MessageReceiverUserDto
               {
                   MessageID = x.MessageID,
                   Subject = x.Subject,
                   Details = x.Details,
                   MessageDate = x.MessageDate,
                   MessageStatus = x.MessageStatus,
                   ReceiverUserName = x.ReceiverUser.UserName,
                   ReceiverNameSurname = x.ReceiverUser.NameSurname,
                   ReceiverImageUrl = x.ReceiverUser.ImageUrl,
                   SenderUserId = x.SenderUserId,
                   ReceiverUserId = x.ReceiverUserId
               });

        query = filter == null ?
           query : query.Where(filter);

        query = query.OrderByDescending(x => x.MessageID).AsQueryable();

        query = take > 0 ? query.Skip(skip).Take(take) : query;

        return await query.AsNoTrackingWithIdentityResolution().ToListAsync(CancellationToken);
    }
}