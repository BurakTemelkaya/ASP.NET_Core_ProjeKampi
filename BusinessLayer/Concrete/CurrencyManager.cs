using BusinessLayer.Abstract;
using BusinessLayer.Constants;
using BusinessLayer.Models;
using CoreLayer.Utilities.Results;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace BusinessLayer.Concrete;

public class CurrencyManager : ICurrencyService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;

    public CurrencyManager(IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
    }

    private CancellationToken CancellationToken => _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

    /// <summary>
    /// Dövizlerin verisini tek tek çekmektense hepsini bi anda çekmemin sebebi sitenin ard arda istek atmasını engellemek.
    /// Aksi taktirde aynı anda çok fazla istek atınca TCMB tarafında 429 hatası alınıyor.
    /// </summary>
    /// <param name="currencys"></param>
    /// <returns></returns>
    // Cache CoreDemo.CurrencyWorker tarafında tutuluyor; burada cache'lenirse worker yenileyemez.
    public async Task<IDataResult<List<CurrencysModel>>> GetCurrencyDataAsync(params string[] currencys)
    {
        try
        {
            string exchangeRate = "https://www.tcmb.gov.tr/kurlar/today.xml";
            var xmlDoc = new XmlDocument();

            using HttpClient client = _httpClientFactory.CreateClient();

            Stream stream = await client.GetStreamAsync(exchangeRate, CancellationToken);

            xmlDoc.Load(stream);

            List<CurrencysModel> model = [];

            foreach (var currency in currencys)
            {
                string xpathQuery = $"Tarih_Date/Currency[@Kod='{currency}']/BanknoteSelling";

                string value = xmlDoc.SelectSingleNode(xpathQuery)?.InnerXml;

                model.Add(new CurrencysModel
                {
                    Code = currency,
                    Value = value?[..5]?.ToString()
                });
            }
            return new SuccessDataResult<List<CurrencysModel>>(model);
        }
        catch (Exception ex)
        {
            return new ErrorDataResult<List<CurrencysModel>>(Messages.ErrorFetchingCurrencyData);
        }
    }
}
