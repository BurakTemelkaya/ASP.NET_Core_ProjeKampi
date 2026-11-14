
using BusinessLayer.Abstract;
using BusinessLayer.Constants;
using BusinessLayer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreDemo.Areas.Admin.ViewComponents.Statistic;

public class Statistic3 : ViewComponent
{
    private readonly IUserBusinessService _userService;
    private readonly INewsLetterService _newsLetterService;
    private readonly ICommentService _commentService;
    private readonly ICategoryService _categoryService;
    private readonly IMemoryCache _memoryCache;
    public Statistic3(IUserBusinessService userService, INewsLetterService newsLetterService, ICommentService commentService,
        ICategoryService categoryService, IMemoryCache memoryCache)
    {
        _userService = userService;
        _newsLetterService = newsLetterService;
        _commentService = commentService;
        _categoryService = categoryService;
        _memoryCache = memoryCache;
    }


    public async Task<IViewComponentResult> InvokeAsync()
    {
        ViewBag.UserCount = (await _userService.GetByUserCountAsync()).Data;
        ViewBag.LikeCommentCount = (await _commentService.GetCountAsync(2)).Data;
        ViewBag.NewsLetterCount = (await _newsLetterService.GetCountAsync()).Data;
        ViewBag.CategoryCount = (await _categoryService.GetCountAsync()).Data;

        // Kurlar CurrencyWorker tarafından arka planda çekilir; burada TCMB beklenmez.
        if (_memoryCache.TryGetValue(CurrencyWorker.CacheKey, out List<CurrencysModel> currencies))
        {
            ViewBag.Euro = currencies.First(x => x.Code == CurrencyCodes.Euro).Value;
            ViewBag.Dolar = currencies.First(x => x.Code == CurrencyCodes.Dollar).Value;
        }
        else
        {
            // Worker'ın ilk çekimi henüz bitmedi (uygulama yeni açıldı).
            ViewBag.Euro = Messages.CurrencyDataLoading;
            ViewBag.Dolar = Messages.CurrencyDataLoading;
        }

        return View();
    }
}
