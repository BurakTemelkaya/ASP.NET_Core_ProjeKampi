using BusinessLayer.Abstract;
using BusinessLayer.Models;
using CoreLayer.Utilities.MailUtilities;
using CoreLayer.Utilities.Results;
using EntityLayer.Concrete;
using EntityLayer.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using X.PagedList;
using X.PagedList.Extensions;

namespace CoreDemo.Controllers;

public class BlogController : Controller
{
    private readonly IBlogService _blogService;
    private readonly IUserBusinessService _businessUserService;
    private readonly ICategoryService _categoryService;

    public BlogController(IBlogService blogService,
        IUserBusinessService businessUserService, ICategoryService categoryService)
    {
        _blogService = blogService;
        _businessUserService = businessUserService;
        _categoryService = categoryService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(GetBlogModel getBlogModel)
    {
        IDataResult<IPagedList<BlogCategoryandCommentCountDto>> values = await _blogService.GetBlogListByMainPage(getBlogModel);

        ViewData["Title"] = "Ana Sayfa";

        if (getBlogModel.Id > 0 || getBlogModel.Search != null)
        {
            ViewData["Title"] = values.Message;
            ViewBag.Message = values.Message;
            ViewBag.IsSuccess = values.Success;
            ViewBag.Search = getBlogModel.Search;
            ViewBag.Id = getBlogModel.Id;
        }

        return View(values.Data);
    }

    [Route("/Blog/BlogReadAll/{title}/{id:int}")]
    [Route("/Blog/BlogReadAll/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> BlogReadAll(int id)
    {
        ViewBag.i = id;
        var value = await _blogService.GetBlogByIdWithCommentAsync(id);
        if (!value.Success)
        {
            return RedirectToAction("Error404", "ErrorPage");
        }
        ViewBag.WriterId = value.Data.WriterID;
        ViewData["Title"] = value.Data.BlogTitle;
        return View(value.Data);
    }
    public async Task<IActionResult> BlogListByWriter(int page = 1)
    {
        var values = await _blogService.GetListWithCategoryByWriterWithPagingAsync(User.Identity.Name, 5, page);
        return View(values.Data);
    }
    [HttpGet]
    public async Task<IActionResult> BlogAdd()
    {
        var categoryList = await _categoryService.GetCategorySelectedListItemAsync(true);
        ViewBag.CategoryList = categoryList.Data;
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> BlogAdd(Blog blog, IFormFile blogImage, IFormFile blogThumbnailImage)
    {
        var value = await _blogService.BlogAddAsync(blog, User.Identity.Name, blogImage, blogThumbnailImage);
        var categoryList = await _categoryService.GetCategorySelectedListItemAsync(true);
        ViewBag.CategoryList = categoryList.Data;
        if (!value.Success)
        {
            ModelState.AddModelError("blogImage", value.Message);
            return View(blog);
        }
        return RedirectToAction("BlogListByWriter", "Blog");
    }
    public async Task<IActionResult> DeleteBlog(int id)
    {
        var blogValue = await _blogService.GetFileNameContentBlogByIDAsync(id);
        await _blogService.DeleteBlogAsync(blogValue.Data, User.Identity.Name);
        return RedirectToAction("BlogListByWriter");
    }
    public async Task<IActionResult> ChangeStatusBlog(int id)
    {
        await _blogService.ChangedBlogStatusAsync(id, User.Identity.Name);
        return RedirectToAction("BlogListByWriter");
    }
    [HttpGet]
    public async Task<IActionResult> EditBlog(int id)
    {
        var blogValue = await _blogService.GetBlogByIdForUpdate(id);

        if (!blogValue.Success)
        {
            return RedirectToAction("BlogListByWriter");
        }

        var categoryList = await _categoryService.GetCategorySelectedListItemAsync(true);
        ViewBag.CategoryList = categoryList.Data;
        return View(blogValue.Data);
    }
    [HttpPost]
    public async Task<IActionResult> EditBlog(Blog blog, IFormFile blogImage, IFormFile blogThumbnailImage)
    {
        var result = await _blogService.BlogUpdateAsync(blog, User.Identity.Name, blogImage, blogThumbnailImage);
        if (!result.Success)
        {
            var categoryList = await _categoryService.GetCategorySelectedListItemAsync(true);
            ViewBag.CategoryList = categoryList.Data;
            ModelState.AddModelError("", result.Message);
            return View(blog);
        }
        return RedirectToAction("BlogListByWriter");
    }
}
