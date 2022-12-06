using System;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using OilPriceFunction.Models;
using StackExchange.Redis;

namespace OilPricesFunction
{
    public class OilPricesFunction
    {
        private readonly ILogger<OilPricesFunction> _logger;
        private readonly IConnectionMultiplexer _redisCache;
        private readonly IConfiguration _configuration;

        public OilPricesFunction(ILogger<OilPricesFunction> log, IConnectionMultiplexer redisCache, IConfiguration configuration)
        {
            _logger = log;
            _redisCache = redisCache;
            _configuration = configuration;
        }

        [FunctionName("OilPricesFunction")]
        [OpenApiOperation(operationId: "Run")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OilPrices), Description = "The oil prices")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
            _logger.LogInformation($"OilPricesFunction started at {DateTime.Now}. Cache expiration set to {_configuration["CacheExpirationInHours"]} hours");
            var db = _redisCache.GetDatabase();
            var json = await db.StringGetAsync("OilPrices");

            OilPrices oilPrices = new OilPrices();

            if (string.IsNullOrEmpty(json))
            {
                _logger.LogInformation("Oil prices not available in cache, getting updated values");
                var url = "https://eapp.kpc.com.kw/oilprices/oilprices.aspx";
                HtmlWeb htmlWeb = new HtmlWeb();
                var htmlDoc = await htmlWeb.LoadFromWebAsync(url);

                try
                {
                    oilPrices.CurrentKec = new OilPrice
                    {
                        Date = htmlDoc.GetElementbyId("lblDateAs").InnerText,
                        Price = htmlDoc.GetElementbyId("lblKEC").InnerText
                    };

                    oilPrices.CurrentButane = new OilPrice
                    {
                        Date = htmlDoc.GetElementbyId("lblMonth21").InnerText,
                        Price = htmlDoc.GetElementbyId("lblButane2").InnerText
                    };

                    oilPrices.PreviousButane = new OilPrice
                    {
                        Date = htmlDoc.GetElementbyId("lblMonth11").InnerText,
                        Price = htmlDoc.GetElementbyId("lblButane1").InnerText
                    };

                    oilPrices.CurrentPropane = new OilPrice
                    {
                        Date = htmlDoc.GetElementbyId("lblMonth2").InnerText,
                        Price = htmlDoc.GetElementbyId("lblPropane2").InnerText
                    };

                    oilPrices.PreviousPropane = new OilPrice
                    {
                        Date = htmlDoc.GetElementbyId("lblMonth1").InnerText,
                        Price = htmlDoc.GetElementbyId("lblPropane1").InnerText
                    };

                    var oilPricesJson = JsonConvert.SerializeObject(oilPrices);
                    int cacheExpireInHours = _configuration.GetValue<int>("CacheExpirationInHours");
                    await db.StringSetAsync("OilPrices", oilPricesJson, TimeSpan.FromHours(cacheExpireInHours));
                    _logger.LogInformation($"Json stored in Redis: {oilPricesJson}");
                }
                catch (Exception exc)
                {
                    _logger.LogError("Error parsing oil prices", exc);
                    oilPrices.Error = exc.Message;
                }
            }
            else
            {
                oilPrices = JsonConvert.DeserializeObject<OilPrices>(json);
                _logger.LogInformation("Oil prices retrieved from Redis cache");
                _logger.LogInformation($"Json read from Redis: {json}");
            }

            return new OkObjectResult(oilPrices);
        }
    }
}

