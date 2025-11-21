using AutoMapper;
using BusinessLayer.Abstract;
using BusinessLayer.Constants;
using BusinessLayer.Models;
using BusinessLayer.StaticTexts;
using BusinessLayer.ValidationRules;
using Core.Extensions;
using CoreLayer.Aspects.AutoFac.Caching;
using CoreLayer.Aspects.AutoFac.Validation;
using CoreLayer.BackgroundTasks;
using CoreLayer.Utilities.Business;
using CoreLayer.Utilities.FileUtilities;
using CoreLayer.Utilities.Results;
using DataAccessLayer.Abstract;
using EntityLayer.Concrete;
using EntityLayer.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using X.PagedList;

namespace BusinessLayer.Concrete;

public class BlogManager : ManagerBase, IBlogService
{
    private readonly IBlogDal _blogDal;
    private readonly IUserBusinessService _userService;
    private readonly ICategoryService _categoryService;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BlogManager(IBlogDal blogDal, IUserBusinessService userService, IMapper mapper, ICategoryService categoryService
        , IHttpContextAccessor contextAccessor, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor, IBackgroundTaskQueue backgroundTaskQueue) : base(mapper)
    {
        _blogDal = blogDal;
        _userService = userService;
        _categoryService = categoryService;
        _contextAccessor = contextAccessor;
        _serviceProvider = serviceProvider;
        _backgroundTaskQueue = backgroundTaskQueue;
        _httpContextAccessor = httpContextAccessor;
    }

    private CancellationToken CancellationToken => _contextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;


    [CacheAspect]
    public async Task<IDataResult<IPagedList<Blog>>> GetListWithCategoryByWriterWithPagingAsync(string userName, int take, int page)
    {
        var user = await _userService.GetByUserNameAsync(userName);
        var values = await _blogDal.GetPagedListAsync(
            page, take, x => x.WriterID == user.Data.Id, include: x => x.Include(b => b.Category), orderBy: x => x.OrderByDescending(b => b.BlogID));

        foreach (var item in values)
        {
            if (item != null)
            {
                item.BlogContent = await TextFileManager.ReadTextFileAsync(item.BlogContent, 50, CancellationToken);
            }
        }
        return new SuccessDataResult<IPagedList<Blog>>(values);
    }

    [CacheAspect]
    public async Task<IDataResult<IPagedList<Blog>>> GetListWithCategoryAsync(bool? status, int take = 0, int skip = 0)
    {
        var values = await _blogDal.GetPagedListAsync(take, skip, x => x.Category.CategoryStatus == status, x => x.Include(b => b.Category), x => x.OrderByDescending(b => b.BlogID));
        return new SuccessDataResult<IPagedList<Blog>>(values);
    }

    public async Task<IDataResult<IPagedList<Blog>>> GetListWithCategoryByPaging(int pageNumber = 1, int pageSize = 10)
    {
        var values = await _blogDal.GetPagedListAsync(pageNumber, pageSize, include: x => x.Include(b => b.Category), orderBy: x => x.OrderByDescending(b => b.BlogID));
        foreach (var item in values)
        {
            if (item != null)
            {
                item.BlogContent = await TextFileManager.ReadTextFileAsync(item.BlogContent, 50);
            }
        }
        return new SuccessDataResult<IPagedList<Blog>>(values);
    }

    public async Task<IDataResult<Blog>> GetBlogByIDAsync(int id)
    {
        var value = await _blogDal.GetByIDAsync(id);
        if (value == null)
            return new ErrorDataResult<Blog>(Messages.BlogNotFound);
        value.BlogContent = await TextFileManager.ReadTextFileAsync(value.BlogContent);
        return new SuccessDataResult<Blog>(value);
    }

    [CacheRemoveAspect("IBlogService.Get")]
    public async Task<IDataResult<Blog>> GetBlogByIdForUpdate(int id)
    {
        var value = await _blogDal.GetByIDAsync(id);

        if (value == null)
            return new ErrorDataResult<Blog>(Messages.BlogNotFound);

        var user = await _userService.GetByUserNameAsync(_contextAccessor.HttpContext.User.Identity.Name);

        var isAdmin = _contextAccessor.HttpContext.User.IsInRole("Admin");

        if (!isAdmin && user.Data.Id != value.WriterID)
            return new ErrorDataResult<Blog>(Messages.BlogDoesNotBelongToYouCannotBeUpdated);

        value.BlogThumbnailImage = null;
        value.BlogImage = null;

        value.BlogContent = await TextFileManager.ReadTextFileAsync(value.BlogContent);
        return new SuccessDataResult<Blog>(value);
    }

    [CacheAspect]
    public async Task<IDataResult<List<Blog>>> GetLastBlogAsync(int count, int skip = 0, bool sortInReverse = true)
    {
        var value = await _blogDal.GetListAllAsync(x => x.BlogStatus, count, skip, sortInReverse);

        if (value == null)
        {
            return new ErrorDataResult<List<Blog>>(Messages.BlogNotFound);
        }

        return new SuccessDataResult<List<Blog>>(value);
    }

    [CacheRemoveAspect("IBlogService.Get")]
    [CacheRemoveAspect("ICategoryService.Get")]
    [ValidationAspect(typeof(BlogValidator))]
    public async Task<IResultObject> BlogAddAsync(Blog blog, string userName, IFormFile blogImage, IFormFile blogThumbnailImage)
    {
        var user = await _userService.GetByUserNameAsync(userName);

        var categoryStatusResult = await CheckCategoryStatusAsync(user, blog);

        if (!categoryStatusResult.Success)
        {
            return categoryStatusResult;
        }

        if (blog.BlogImage != null)
        {
            var image = await ImageFileManager.DownloadImageAsync(blog.BlogImage);
            if (image == null)
            {
                return new ErrorResult(Messages.BlogImageNotGetting);
            }
            blog.BlogImage = await ImageFileManager.ImageAddAsync(image, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogImageResolution(), cancellationToken: CancellationToken);
        }
        else if (blogImage != null)
        {
            blog.BlogImage = await ImageFileManager.ImageAddAsync(blogImage, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogImageResolution(), cancellationToken: CancellationToken);
        }

        if (blog.BlogImage == null)
        {
            return new ErrorResult(Messages.BlogImageNotUploading);
        }

        if (blog.BlogThumbnailImage != null)
        {
            var image = await ImageFileManager.DownloadImageAsync(blog.BlogThumbnailImage, cancellationToken: CancellationToken);
            if (image == null)
            {
                return new ErrorResult(Messages.BlogThumbnailNotGetting);
            }
            blog.BlogThumbnailImage = await ImageFileManager.ImageAddAsync(image, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogThumbnailResolution(), cancellationToken: CancellationToken);
        }
        else if (blogThumbnailImage != null)
        {
            blog.BlogThumbnailImage = await ImageFileManager.ImageAddAsync(blogThumbnailImage, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogThumbnailResolution(), cancellationToken: CancellationToken);
        }

        if (blog.BlogThumbnailImage == null)
        {
            return new ErrorResult(Messages.BlogThumbnailNotUploading);
        }

        var result = BusinessRules.Run(UserNotEmpty(user), BlogImageNotEmpty(blog.BlogImage), BlogThumbnailNotEmpty(blog.BlogThumbnailImage));

        if (!result.Success)
        {
            return result;
        }

        blog.BlogContent = await TextFileManager.TextFileAddAsync(blog.BlogContent, ContentFileLocations.GetBlogContentFileLocation(), ContentFileLocations.GetBlogContentImagesFileLocation(), cancellationToken: CancellationToken);
        blog.WriterID = user.Data.Id;
        blog.BlogCreateDate = DateTime.Now;
        await _blogDal.InsertAsync(blog);
        return new SuccessResult();
    }

    [CacheRemoveAspect("IBlogService.Get")]
    [CacheRemoveAspect("ICategoryService.Get")]
    [ValidationAspect(typeof(BlogValidator))]
    public async Task<IResultObject> BlogUpdateAsync(Blog blog, string userName, IFormFile blogImage = null, IFormFile blogThumbnailImage = null)
    {
        var user = await _userService.GetByUserNameAsync(userName);
        var oldValueRaw = await GetBlogByIDAsync(blog.BlogID);
        var oldValue = oldValueRaw.Data;

        var categoryStatusResult = await CheckCategoryStatusAsync(user, blog);

        if (!categoryStatusResult.Success)
        {
            return categoryStatusResult;
        }

        blog.WriterID = oldValue.WriterID;
        blog.BlogCreateDate = oldValue.BlogCreateDate;

        if (user == null || user.Data.Id != oldValue.WriterID)
            return new ErrorDataResult<Blog>(blog, Messages.BlogDoesNotBelongToYouCannotBeUpdated);

        if (blog.BlogImage == null && blogImage == null)
        {
            blog.BlogImage = oldValue.BlogImage;
        }
        else if (blog.BlogImage != null)
        {
            var image = await ImageFileManager.DownloadImageAsync(blog.BlogImage, cancellationToken: CancellationToken);
            if (image == null)
            {
                return new ErrorResult(Messages.BlogImageNotGetting);
            }
            blog.BlogImage = await ImageFileManager.ImageAddAsync(image, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogImageResolution(), cancellationToken: CancellationToken);
        }
        else if (blogImage != null)
        {
            DeleteFileManager.DeleteFile(oldValue.BlogImage);
            blog.BlogImage = await ImageFileManager.ImageAddAsync(blogImage, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogImageResolution(), cancellationToken: CancellationToken);
        }

        if (blog.BlogImage == null)
        {
            return new ErrorResult(Messages.BlogImageNotUploading);
        }


        if (blog.BlogThumbnailImage == null && blogThumbnailImage == null)
        {
            blog.BlogThumbnailImage = oldValue.BlogThumbnailImage;
        }
        else if (blog.BlogThumbnailImage != null)
        {
            var image = await ImageFileManager.DownloadImageAsync(blog.BlogThumbnailImage, cancellationToken: CancellationToken);
            if (image == null)
            {
                return new ErrorResult(Messages.BlogThumbnailNotGetting);
            }
            blog.BlogThumbnailImage = await ImageFileManager.ImageAddAsync(image, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogThumbnailResolution(), cancellationToken: CancellationToken);
            DeleteFileManager.DeleteFile(oldValue.BlogContent);
        }
        else if (blogThumbnailImage != null)
        {
            blog.BlogThumbnailImage = await ImageFileManager.ImageAddAsync(blogThumbnailImage, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogThumbnailResolution(), cancellationToken: CancellationToken);
            if (blog.BlogThumbnailImage == null)
            {
                return new ErrorResult(Messages.BlogThumbnailNotGetting);
            }
            DeleteFileManager.DeleteFile(oldValue.BlogContent);
        }

        if (blog.BlogThumbnailImage == null)
        {
            return new ErrorResult(Messages.BlogThumbnailNotUploading);
        }


        var oldBlogValue = await GetFileNameContentBlogByIDAsync(blog.BlogID);
        if (blog.BlogContent != oldValue.BlogContent)
        {
            blog.BlogContent = await TextFileManager.TextFileAddAsync(blog.BlogContent, ContentFileLocations.GetBlogContentFileLocation(), ContentFileLocations.GetBlogContentImagesFileLocation(), cancellationToken: CancellationToken);
        }
        else
            blog.BlogContent = oldBlogValue.Data.BlogContent;
        await _blogDal.UpdateAsync(blog);
        return new SuccessResult();
    }

    [CacheRemoveAspect("IBlogService.Get")]
    [CacheRemoveAspect("ICategoryService.Get")]
    [ValidationAspect(typeof(BlogValidator))]
    public async Task<IResultObject> BlogAdminUpdateAsync(Blog blog, IFormFile blogImage = null, IFormFile blogThumbnailImage = null)
    {
        IDataResult<Blog> OldValueRaw = await GetBlogByIDAsync(blog.BlogID);
        Blog oldValue = OldValueRaw.Data;

        blog.WriterID = oldValue.WriterID;
        blog.BlogCreateDate = oldValue.BlogCreateDate;

        if (blog.BlogImage == null && blogImage == null)
        {
            blog.BlogImage = oldValue.BlogImage;
        }
        else if (blog.BlogImage != null)
        {
            IFormFile image = await ImageFileManager.DownloadImageAsync(blog.BlogImage, cancellationToken: CancellationToken);
            if (image == null)
            {
                return new ErrorResult(Messages.BlogImageNotGetting);
            }
            blog.BlogImage = await ImageFileManager.ImageAddAsync(image, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogImageResolution(), cancellationToken: CancellationToken);
        }
        else if (blogImage != null)
        {
            DeleteFileManager.DeleteFile(oldValue.BlogImage);
            blog.BlogImage = await ImageFileManager.ImageAddAsync(blogImage, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogImageResolution(), cancellationToken: CancellationToken);
        }

        if (blog.BlogImage == null)
        {
            return new ErrorResult(Messages.BlogImageNotUploading);
        }

        if (blog.BlogThumbnailImage == null && blogThumbnailImage == null)
        {
            blog.BlogThumbnailImage = oldValue.BlogThumbnailImage;
        }
        else if (blog.BlogThumbnailImage != null)
        {
            IFormFile image = await ImageFileManager.DownloadImageAsync(blog.BlogThumbnailImage, cancellationToken: CancellationToken);
            if (image == null)
            {
                return new ErrorResult(Messages.BlogThumbnailNotGetting);
            }
            blog.BlogThumbnailImage = await ImageFileManager.ImageAddAsync(image, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogThumbnailResolution(), cancellationToken: CancellationToken);

            DeleteFileManager.DeleteFile(oldValue.BlogThumbnailImage);
        }
        else if (blogThumbnailImage != null)
        {
            blog.BlogThumbnailImage = await ImageFileManager.ImageAddAsync(blogThumbnailImage, ContentFileLocations.StaticBlogImageLocation(), ImageResulotions.GetBlogThumbnailResolution(), cancellationToken: CancellationToken
                );
            DeleteFileManager.DeleteFile(oldValue.BlogThumbnailImage);
        }

        if (blog.BlogThumbnailImage == null)
        {
            return new ErrorResult(Messages.BlogThumbnailNotUploading);
        }


        var oldBlogValue = await GetFileNameContentBlogByIDAsync(blog.BlogID);
        if (blog.BlogContent != oldValue.BlogContent)
        {
            blog.BlogContent = await TextFileManager.TextFileAddAsync(blog.BlogContent, ContentFileLocations.GetBlogContentFileLocation(), ContentFileLocations.GetBlogContentImagesFileLocation(), cancellationToken: CancellationToken);
        }
        else
            blog.BlogContent = oldBlogValue.Data.BlogContent;
        await _blogDal.UpdateAsync(blog);

        return new SuccessResult();
    }

    [CacheAspect]
    public async Task<IDataResult<int>> GetCountAsync(bool? blogStatus)
    {
        return new SuccessDataResult<int>(blogStatus == null ? await _blogDal.GetCountAsync()
            : await _blogDal.GetCountAsync(x => x.BlogStatus == blogStatus));
    }

    [CacheAspect]
    public async Task<IDataResult<int>> GetBlogCountByWriterAsync(string userName)
    {
        var user = await _userService.GetByUserNameAsync(userName);
        if (!user.Success)
        {
            return new ErrorDataResult<int>(Messages.UserNotFound);
        }
        return new SuccessDataResult<int>(await _blogDal.GetCountAsync(x => x.WriterID == user.Data.Id));
    }

    [CacheAspect]
    public async Task<IDataResult<List<Blog>>> GetListByReadAllLastBlogsByWriterAsync(int blogId, int writerID, int take = 0)
    {
        var values = await _blogDal.GetListAllAsync(x => x.BlogID != blogId && x.WriterID == writerID, take);
        foreach (var item in values)
            item.BlogContent = await TextFileManager.ReadTextFileAsync(item.BlogContent, 50, cancellationToken: CancellationToken);
        return new SuccessDataResult<List<Blog>>(values.OrderByDescending(x => x.BlogID).ToList());
    }

    [CacheAspect]
    public async Task<IDataResult<List<Blog>>> GetListByReadAllLastBlogsAsync(int blogId, int writerID, int take = 0, bool isActive = true)
    {
        var values = await _blogDal.GetListAllAsync(x => x.BlogID != blogId && x.WriterID != writerID && x.BlogStatus == isActive, take);
        foreach (var item in values)
            item.BlogContent = await TextFileManager.ReadTextFileAsync(item.BlogContent, 50, cancellationToken: CancellationToken);
        return new SuccessDataResult<List<Blog>>(values.OrderByDescending(x => x.BlogID).ToList());
    }

    [CacheRemoveAspect("ICategoryService.Get")]
    [CacheRemoveAspect("IBlogService.Get")]
    public async Task<IResultObject> ChangedBlogStatusAsync(int id, string userName)
    {
        var blog = await GetFileNameContentBlogByIDAsync(id);
        var user = await _userService.GetByUserNameAsync(userName);
        if (user.Data.Id == blog.Data.WriterID)
        {
            var value = await _blogDal.GetByIDAsync(blog.Data.BlogID);
            if (value.BlogStatus)
                value.BlogStatus = false;
            else
                value.BlogStatus = true;

            await _blogDal.UpdateAsync(value);
            return new SuccessResult();
        }
        return new ErrorResult(Messages.UserNotFound);
    }

    public async Task<IDataResult<Blog>> GetFileNameContentBlogByIDAsync(int id)
    {
        return new SuccessDataResult<Blog>(await _blogDal.GetByIDAsync(id));
    }

    [CacheRemoveAspect("ICategoryService.Get")]
    [CacheRemoveAspect("IBlogService.Get")]
    public async Task<IResultObject> DeleteBlogAsync(Blog blog, string userName)
    {
        var user = await _userService.GetByUserNameAsync(userName);
        if (user.Data.Id == blog.WriterID)
        {
            await TextFileManager.DeleteContentImageFiles(blog.BlogContent);
            DeleteFileManager.DeleteFile(blog.BlogContent);
            DeleteFileManager.DeleteFile(blog.BlogThumbnailImage);
            DeleteFileManager.DeleteFile(blog.BlogImage);

            await _blogDal.DeleteAsync(blog);
            return new SuccessResult();
        }
        return new ErrorResult();
    }

    [CacheRemoveAspect("ICategoryService.Get")]
    [CacheRemoveAspect("IBlogService.Get")]
    public async Task<IResultObject> DeleteBlogByAdminAsync(Blog blog)
    {
        await TextFileManager.DeleteContentImageFiles(blog.BlogContent);
        DeleteFileManager.DeleteFile(blog.BlogContent);
        DeleteFileManager.DeleteFile(blog.BlogThumbnailImage);
        DeleteFileManager.DeleteFile(blog.BlogImage);

        await _blogDal.DeleteAsync(blog);
        return new SuccessResult();
    }

    [CacheRemoveAspect("ICategoryService.Get")]
    [CacheRemoveAspect("IBlogService.Get")]
    public async Task<IResultObject> ChangedBlogStatusByAdminAsync(int id)
    {
        var blog = await GetFileNameContentBlogByIDAsync(id);
        var value = await _blogDal.GetByIDAsync(blog.Data.BlogID);
        if (value.BlogStatus)
            value.BlogStatus = false;
        else
            value.BlogStatus = true;
        await _blogDal.UpdateAsync(value);

        return new SuccessResult();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id">Kategori id değerine göre filtreleme yapmak için gerekli değer.</param>
    /// <param name="page">O anki sayfa sayısını almak ve sayfalama sistemi kullanabilmek için gerekli parametre.</param>
    /// <param name="take">Page paramatresi 0'dan büyük ise sayfalama değil ise verilen sayı kadar veri gelmesini</param>
    /// <param name="search">Bloglar içinde başlığa göre filtreleme yapmak için gerekli parametre.</param>
    /// <returns></returns>
    [ValidationAspect(typeof(GetBlogModelValidator))]
    [CacheAspect]
    public async Task<IDataResult<IPagedList<BlogCategoryandCommentCountDto>>> GetBlogListByMainPage(GetBlogModel getBlogModel)
    {
        IPagedList<BlogCategoryandCommentCountDto> values = null;

        bool isSuccess = true;

        string message = string.Empty;

        values = await _blogDal.GetBlogListWithCategoryandCommentCountAsync(
                    x => (x.BlogStatus == true && x.CategoryStatus == true)
                    && (getBlogModel.Id < 1 || x.CategoryID == getBlogModel.Id)
                    && (string.IsNullOrEmpty(getBlogModel.Search) || x.BlogTitle.ToLower().Contains(getBlogModel.Search.ToLower()))
                    , true, getBlogModel.Page, getBlogModel.Take
                );

        if (!values.Any())
        {
            message = "Arama kriterlerinize uygun sonuç bulunamadı.";
            isSuccess = false;
            values = await _blogDal.GetBlogListWithCategoryandCommentCountAsync(x => x.BlogStatus
            && x.CategoryStatus, true, getBlogModel.Page, getBlogModel.Take);
        }
        else
        {
            if (getBlogModel.Id > 0)
            {
                var category = await _categoryService.TGetByIDAsync((int)getBlogModel.Id);

                message = category.Success ? category.Data.CategoryName + " kategorisindeki bloglar. \n" : string.Empty;
            }

            message += getBlogModel.Search != null ? " Aranan kelime = " + getBlogModel.Search : "";
        }

        foreach (var item in values)
        {
            if (item != null)
            {
                item.BlogContent = await TextFileManager.ReadTextFileAsync(item.BlogContent, 50, cancellationToken: CancellationToken);
            }
        }

        return new DataResult<IPagedList<BlogCategoryandCommentCountDto>>(values, isSuccess, message);
    }

    public async Task<IDataResult<BlogCategoryandCommentCountandWriterDto>> GetBlogByIdWithCommentAsync(int id)
    {
        var result = await _blogDal.GetBlogWithCommentandWriterAsync(true, x => x.BlogID == id);
        if (result == null)
        {
            return new ErrorDataResult<BlogCategoryandCommentCountandWriterDto>(Messages.BlogNotFound);
        }

        var rule = BusinessRules.Run(await BlogReadAuthorizeCheck(result, result.CategoryStatus));
        if (!rule.Success)
        {
            return new ErrorDataResult<BlogCategoryandCommentCountandWriterDto>(rule.Message);
        }

        result.BlogContent = await TextFileManager.ReadTextFileAsync(result.BlogContent, cancellationToken: CancellationToken);

        HttpContext httpContext = _httpContextAccessor.HttpContext;

        await _backgroundTaskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            using var scope = _serviceProvider.CreateScope();
            IBlogViewService blogViewService = scope.ServiceProvider.GetRequiredService<IBlogViewService>();
            await blogViewService.AddAsync(id, httpContext);
        });

        return new SuccessDataResult<BlogCategoryandCommentCountandWriterDto>(result);
    }

    IResultObject BlogIsNotEmpty(Blog blog)
    {
        if (blog == null)
        {
            return new ErrorResult(Messages.BlogNotFound);
        }
        return new SuccessResult();
    }

    async Task<IResultObject> BlogReadAuthorizeCheck(Blog blog, bool categoryStatus)
    {
        IResultObject rule = BusinessRules.Run(BlogIsNotEmpty(blog));

        if (!rule.Success)
        {
            return rule;
        }

        if (!blog.BlogStatus || !categoryStatus)
        {
            var sessionUser = _contextAccessor.HttpContext.User;

            if (sessionUser.Identity.Name == null)
            {
                return new ErrorResult(Messages.UserNotFound);
            }

            foreach (var role in sessionUser.ClaimRoles())
            {
                if (role == "Admin" || role == "Moderatör")
                {
                    return new SuccessResult();
                }
            }

            var user = await _userService.GetByUserNameAsync(_contextAccessor.HttpContext.User.Identity.Name);

            if (!user.Success)
            {
                return new ErrorResult(user.Message);
            }
            else if (user.Data.Id == blog.WriterID)
            {
                return new SuccessResult();
            }

            return new ErrorResult(Messages.BlogViewAuthorizeError);
        }
        return new SuccessResult();
    }

    IResultObject BlogImageNotEmpty(string blogImage)
    {
        if (blogImage == string.Empty)
        {
            return new ErrorResult(Messages.BlogImageNotEmpty);
        }
        return new SuccessResult();
    }

    IResultObject BlogThumbnailNotEmpty(string blogThumnailImage)
    {
        if (blogThumnailImage == string.Empty)
        {
            return new ErrorResult(Messages.BlogThumbnailNotEmpty);
        }
        return new SuccessResult();
    }

    async Task<IResultObject> CheckCategoryStatusAsync(IDataResult<UserDto> user, Blog blog)
    {
        var roles = await _userService.GetUserRoleListAsync(user.Data);

        bool isAdminorModerator = roles.Data.Count(x => x == RolesTexts.AdminRole() || x == RolesTexts.ModeratorRole()) > 0;

        if (!isAdminorModerator)
        {
            //kategori pasif ise bu kategoride adminler hariç blog eklenemez.
            var categoryList = await _categoryService.GetListAsync();
            var category = categoryList.Data.Find(x => x.CategoryID == blog.CategoryID);
            if (!category.CategoryStatus)
            {
                return new ErrorResult(Messages.BlogCategoryIsPassiveNotAdded);
            }
        }
        return new SuccessResult();
    }

    /// <summary>
    /// Resim isimlerinin sadece guid kalacak şekilde yeniden adlandırılmasını sağlayan metod.
    /// </summary>
    /// <returns></returns>
    private async Task MoveBlogImageFileAsync()
    {
        var blogs = await _blogDal.GetListAllAsync();

        foreach (var blog in blogs)
        {
            try
            {
                if (blog.BlogImage == null || blog.BlogImage == string.Empty)
                    continue;

                int index = blog.BlogImage.LastIndexOf('-', blog.BlogImage.Length - 41);

                string oldName = blog.BlogImage;

                string newName = blog.BlogImage.Substring(index + 1);

                bool isMove = await FileManager.FileMoveAsync(oldName, newName, cancellationToken: CancellationToken);

                if (isMove)
                    blog.BlogImage = newName;

                if (blog.BlogThumbnailImage == null || blog.BlogThumbnailImage == string.Empty)
                    continue;


                int indexThumbnail = blog.BlogThumbnailImage.LastIndexOf('-', blog.BlogThumbnailImage.Length - 41);

                string oldNameThumbnail = blog.BlogThumbnailImage;

                string newNameThumbnail = blog.BlogThumbnailImage.Substring(indexThumbnail + 1);

                bool isMoveThumbnail = await FileManager.FileMoveAsync(oldNameThumbnail, newNameThumbnail, cancellationToken: CancellationToken);

                if (isMoveThumbnail)
                    blog.BlogThumbnailImage = newNameThumbnail;
            }
            catch
            {
                continue;
            }
        }

        await _blogDal.UpdateRangeAsync(blogs);
    }
}
