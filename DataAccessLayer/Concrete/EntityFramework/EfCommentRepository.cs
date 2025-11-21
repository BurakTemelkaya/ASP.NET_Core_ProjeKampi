using CoreLayer.DataAccess;
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

public class EfCommentRepository : EfEntityRepositoryBase<Comment>, ICommentDal
{

    public EfCommentRepository(Context context) : base(context)
    {
        Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    private Context Context => _context as Context;

    private CancellationToken CancellationToken =>
        base.HttpContextAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None;

    public async Task<List<Comment>> GetListWithCommentByBlogAsync(Expression<Func<Comment, bool>> filter = null, int take = 0, int skip = 0)
    {
        var query = from comment in Context.Comments
                    join blog in Context.Blogs on comment.BlogID equals blog.BlogID into joinedBlog
                    from subBlog in joinedBlog.DefaultIfEmpty()
                    select new Comment
                    {
                        CommentID = comment.CommentID,
                        CommentContent = comment.CommentContent,
                        BlogID = comment.BlogID,
                        BlogScore = comment.BlogScore,
                        CommentDate = comment.CommentDate,
                        CommentStatus = comment.CommentStatus,
                        CommentTitle = comment.CommentTitle,
                        CommentUserName = comment.CommentUserName,
                        Blog = subBlog
                    };

        query = query.OrderByDescending(comment => comment.CommentID);

        if (filter != null)
            query = query.Where(filter);

        if (take > 0)
            query = query.Skip(skip).Take(take);

        return await query.AsNoTracking().ToListAsync(CancellationToken);

    }

    public async Task<List<Comment>> GetListWithCommentByBlogandPagingAsync(Expression<Func<Comment, bool>> filter = null, int take = 0, int page = 1)
    {
        int skip = 0;
        if (page > 1)
        {
            skip = take * (page - 1);
        }

        int count = await GetCountAsync(filter);

        if (skip >= count)
        {
            skip = 0;
            page = 1;
        }

        return AddNullObject<Comment>.GetListByPaging(await GetListWithCommentByBlogAsync(filter, take, skip), take, page, count);
    }

    public async Task<Comment> GetCommentByBlog(Expression<Func<Comment, bool>> filter = null)
    {
        return filter == null ?
            await Context.Comments.Include(x => x.Blog).FirstAsync(CancellationToken) :
            await Context.Comments.Include(x => x.Blog).FirstAsync(filter, CancellationToken);
    }
}
