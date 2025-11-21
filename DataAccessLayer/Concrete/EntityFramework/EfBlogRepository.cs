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
using X.PagedList;
using X.PagedList.EF;

namespace DataAccessLayer.Concrete.EntityFramework;

public class EfBlogRepository : EfEntityRepositoryBase<Blog>, IBlogDal
{

    public EfBlogRepository(Context context) : base(context)
    {
        Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    private Context Context => _context as Context;

    private CancellationToken CancellationToken =>
        HttpContextAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None;

    public async Task<IPagedList<BlogCategoryandCommentCountDto>> GetBlogListWithCategoryandCommentCountAsync(
    Expression<Func<BlogCategoryandCommentCountDto, bool>> filter = null,
    bool commentStatus = true,
    int pageNumber = 0,
    int pageSize = 0)
    {
        var query = Context.Blogs
            .Include(b => b.Category)
            .Include(b => b.Comments.Where(c => c.CommentStatus == commentStatus))
            .Include(b => b.BlogViews)
            .Select(blog => new BlogCategoryandCommentCountDto
            {
                BlogID = blog.BlogID,
                BlogContent = blog.BlogContent,
                BlogImage = blog.BlogImage,
                BlogThumbnailImage = blog.BlogThumbnailImage,
                BlogTitle = blog.BlogTitle,
                BlogCreateDate = blog.BlogCreateDate,
                BlogStatus = blog.BlogStatus,
                CategoryID = blog.Category.CategoryID,
                CategoryName = blog.Category.CategoryName,
                CategoryStatus = blog.Category.CategoryStatus,
                CommentCount = blog.Comments.Count(),
                CommentScore = blog.Comments.Average(c => (double?)c.BlogScore) ?? 0,
                BlogViewCount = blog.BlogViews.Count()
            });

        query = query.OrderByDescending(x => x.BlogID);

        if (filter != null)
        {
            query = query.Where(filter);
        }

        var result = await query.AsNoTrackingWithIdentityResolution().ToPagedListAsync(pageNumber, pageSize, null, CancellationToken);

        return result;
    }

    public async Task<IPagedList<Blog>> GetListBlogWithCategoryAsync(Expression<Func<Blog, bool>> filter = null, int pageNumber = 1, int pageSize = 10)
    {
        var query = Context.Blogs.Include(x => x.Category).AsQueryable();

        query = filter != null ?
            query.Where(filter)
            : query;

        query = query.OrderByDescending(x => x.BlogID);

        return await query.AsNoTrackingWithIdentityResolution().ToPagedListAsync(pageNumber, pageSize,null, CancellationToken);
    }

    public async Task<BlogCategoryandCommentCountandWriterDto> GetBlogWithCommentandWriterAsync(bool isCommentStatus, Expression<Func<BlogCategoryandCommentCountandWriterDto, bool>> filter)
    {
        IQueryable<BlogCategoryandCommentCountandWriterDto> query = from blog in Context.Blogs
                    join category in Context.Categories
                        on blog.CategoryID equals category.CategoryID
                    join writer in Context.Users
                        on blog.WriterID equals writer.Id
                    join blogView in Context.BlogViews
                        on blog.BlogID equals blogView.BlogId into BlogViews
                    from blogView in BlogViews.DefaultIfEmpty()
                    let commentCount = Context.Comments.Count(x => x.BlogID == blog.BlogID && x.CommentStatus == isCommentStatus)
                    let commentScore = Context.Comments.Where(x => x.BlogID == blog.BlogID && x.CommentStatus == isCommentStatus).Average(x => (double?)x.BlogScore) ?? 0
                    select new BlogCategoryandCommentCountandWriterDto
                    {
                        BlogID = blog.BlogID,
                        BlogContent = blog.BlogContent,
                        BlogImage = blog.BlogImage,
                        BlogThumbnailImage = blog.BlogThumbnailImage,
                        BlogTitle = blog.BlogTitle,
                        BlogCreateDate = blog.BlogCreateDate,
                        BlogStatus = blog.BlogStatus,
                        CategoryID = blog.Category.CategoryID,
                        CategoryName = blog.Category.CategoryName,
                        CategoryStatus = category.CategoryStatus,
                        CommentCount = commentCount,
                        CommentScore = commentScore,
                        CommentStatus = isCommentStatus,
                        WriterID = writer.Id,
                        WriterNameSurName = writer.NameSurname,
                        WriterUserName = writer.UserName,
                        BlogViewCount = blog.BlogViews.Count()
                    };

        var data = await query.Where(filter).AsNoTracking().FirstOrDefaultAsync(CancellationToken);

        return data;
    }

    public async Task<int> GetCountByBlogCategoryandCommentCountAsync(Expression<Func<BlogCategoryandCommentCountDto, bool>> filter = null)
    {
        var query = Context.Blogs.Select(data =>
            new BlogCategoryandCommentCountDto
            {
                BlogID = data.BlogID,
                BlogContent = data.BlogContent,
                BlogImage = data.BlogImage,
                BlogTitle = data.BlogTitle,
                BlogCreateDate = data.BlogCreateDate,
                BlogThumbnailImage = data.BlogThumbnailImage,
                BlogStatus = data.BlogStatus,
                CategoryID = data.CategoryID,
                CategoryName = data.Category.CategoryName,
                CategoryStatus = data.Category.CategoryStatus
            });

        query = filter != null ? query.Where(filter) : query;

        return await query.AsNoTracking().CountAsync(CancellationToken);
    }
}