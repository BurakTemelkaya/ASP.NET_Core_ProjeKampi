using BusinessLayer.Abstract;
using CoreDemo.Areas.Admin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CoreDemo.Areas.Admin.ViewComponents.Statistic;

public class Statistic1 : ViewComponent
{
    private readonly IBlogService _blogService;
    private readonly IMessageService _messageService;
    private readonly ICommentService _commentService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;

    public Statistic1(IBlogService blogService, IMessageService message2Service, ICommentService commentService, IConfiguration configuration, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
        _blogService = blogService;
        _messageService = message2Service;
        _commentService = commentService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        XAttribute weatherApi = await WeatherApiDataAsync();

        string tempCelcius = weatherApi != null ? weatherApi.Value : "Veri alınırken hata oluştu.";

        tempCelcius = tempCelcius.Length == 5 ? tempCelcius[..4] : tempCelcius[..3];

        var blogCount = await _blogService.GetCountAsync();

        var messageCount = await _messageService.GetCountAsync();

        var commentCount = await _commentService.GetCountAsync();

        WidgetModel widgetModel = new WidgetModel
        {
            BlogCount = blogCount.Data,
            MessageCount = messageCount.Data,
            CommentCount = commentCount.Data,
            Temparature = tempCelcius
        };
        return View(widgetModel);
    }
    private async Task<XAttribute?> WeatherApiDataAsync(string city = "istanbul")
    {
        if (_memoryCache.TryGetValue($"Weather_{city}", out XAttribute cached))
            return cached;

        try
        {
            string apiKey = _configuration["OpenWeatherApiKeys:Key"];

            string connection = $"https://api.openweathermap.org/data/2.5/weather?q={city}&mode=xml&appid={apiKey}&units=metric";

            HttpClient client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5); // API yanıt vermezse panel 100 sn (varsayılan) beklemesin

            Stream stream = await client.GetStreamAsync(connection);

            XDocument document = await XDocument.LoadAsync(stream, LoadOptions.None, HttpContext.RequestAborted);

            var temperature = document.Descendants("temperature").FirstOrDefault()?.Attribute("value");

            if (temperature != null)
                _memoryCache.Set($"Weather_{city}", temperature, TimeSpan.FromMinutes(30));

            return temperature;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
