using BusinessLayer.Abstract;
using BusinessLayer.Constants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreDemo;

/// <summary>
/// TCMB kur XML'i ~10 sn sürebildiği için kurlar arka planda çekilip cache'te hazır tutulur,
/// admin paneli isteği hiçbir zaman TCMB'yi beklemez.
/// </summary>
public class CurrencyWorker(ICurrencyService currencyService, IMemoryCache memoryCache) : BackgroundService
{
    public const string CacheKey = "Currencies";

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await currencyService.GetCurrencyDataAsync(CurrencyCodes.Euro, CurrencyCodes.Dollar);

            // Süresiz tutulur: TCMB geçici olarak cevap vermezse son bilinen kur gösterilir.
            if (result.Success)
                memoryCache.Set(CacheKey, result.Data);

            await Task.Delay(result.Success ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(1), cancellationToken);
        }
    }
}
