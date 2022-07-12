using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using RateLimiting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RateLimiting.Middlewares
{
    public class RateLimitMiddleware : IMiddleware
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        public RateLimitMiddleware(IConfiguration configuration, IMemoryCache cache)
        {
            _configuration = configuration;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var rules = _configuration.GetSection("Rules").Get<List<RuleModel>>();

            var endpoint = context.GetEndpoint();

            var countryCode = await GetIpInfo(context);

            if (string.IsNullOrEmpty(countryCode))
            {
                await next(context);
                return;
            }

            var rule = rules.FirstOrDefault(e => e.CountryCode.ToLower() == countryCode.ToLower());

            if (rule != null)
            {
                var key = GenerateClientKey(context);

                var clientInfos = GetClientInfoByKey(key);

                if (clientInfos != null &&
                       DateTime.UtcNow < clientInfos.LastSuccessfulResponseTime.AddSeconds(rule.TimeLimitation) &&
                       clientInfos.NumberOfRequestsCompletedSuccessfully == rule.MaxRequests)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    return;
                }

                UpdateClientInfo(key, rule.MaxRequests);
                await next(context);
            }
            else
            {
                await next(context);
                return;
            }
        }

        private async Task<string> GetIpInfo(HttpContext context)
        {
            // 
            // https://www.techieclues.com/blogs/how-to-get-free-geolocation-from-ip-address-using-csharp

            var clientIp = context.Connection.RemoteIpAddress.ToString();

             clientIp = "162.254.206.227";  // for test US ip address

            var Ip_Api_Url = $"http://ip-api.com/json/{clientIp}";

            // Use HttpClient to get the details from the Json response
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Pass API address to get the Geolocation details 
                httpClient.BaseAddress = new Uri(Ip_Api_Url);
                HttpResponseMessage httpResponse = httpClient.GetAsync(Ip_Api_Url).GetAwaiter().GetResult();

                // If API is success and receive the response, then get the location details
                if (httpResponse.IsSuccessStatusCode)
                {
                    var geolocationInfo = await JsonSerializer.DeserializeAsync<LocationDetails>(await httpResponse.Content.ReadAsStreamAsync());

                    if (geolocationInfo != null)
                    {
                        return geolocationInfo.countryCode;
                    }
                }

                return string.Empty;
            }
        }

        private static string GenerateClientKey(HttpContext context)
        {

            var remoteAddr = context.Connection.RemoteIpAddress.ToString();
            remoteAddr = "162.254.206.227"; // for test

            return $"{context.Request.Path}_{remoteAddr}";
        }

        private ClientCallInfo GetClientInfoByKey(string key)
        {
            return _cache.Get<ClientCallInfo>(key);
        }

        private void UpdateClientInfo(string key, int maxRequests)
        {
            var clientStat = _cache.Get<ClientCallInfo>(key);

            if (clientStat != null)
            {
                clientStat.LastSuccessfulResponseTime = DateTime.UtcNow;

                if (clientStat.NumberOfRequestsCompletedSuccessfully == maxRequests)
                    clientStat.NumberOfRequestsCompletedSuccessfully = 1;

                else
                    clientStat.NumberOfRequestsCompletedSuccessfully++;

                _cache.Set<ClientCallInfo>(key, clientStat);
            }
            else
            {
                var clientCallInfo = new ClientCallInfo
                {
                    LastSuccessfulResponseTime = DateTime.UtcNow,
                    NumberOfRequestsCompletedSuccessfully = 1
                };

                _cache.Set<ClientCallInfo>(key, clientCallInfo);
            }

        }
    }
}
