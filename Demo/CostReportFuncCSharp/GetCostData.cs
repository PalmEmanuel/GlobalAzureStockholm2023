using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace PipeHow.CostReportFuncCSharp;

public static class GetCostData
{
    private static readonly HttpClient client = new();

    [FunctionName("GetCostData")]
    [return: Queue("%CostQueueName%")]
    public static async Task<string> Run(
        [TimerTrigger("0 0 6 * * *")] TimerInfo timer, ILogger log) // Run daily at 6AM
    {
        try
        {
            // Get access token
            var token = await GetManagedIdentityToken();

            // Get cost data from Cost Management API
            var costDataString = await GetCostManagementJsonData(token);
            log.LogInformation($"""
            Cost data: {costDataString}
            Next run: {timer.ScheduleStatus.Next:yyyy-MM-dd}
            """);

            // Post data as string to storage queue by returning it
            return costDataString;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not get cost management data!");
            throw;
        }
    }

    public static async Task<string> GetCostManagementJsonData(string token)
    {
        // Assemble URL for Cost Management API
        string scope = Environment.GetEnvironmentVariable("CostScope");
        string url = $"https://management.azure.com/{scope}/providers/Microsoft.CostManagement/query?api-version=2022-10-01";
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var from = DateTime.UtcNow.AddDays(-2);
        var to = DateTime.UtcNow.AddDays(-1);

        // Create JSON body using C# 11 multiline string interpolation
        string body = $$"""
        {
            "type": "ActualCost",
            "dataset": {
                "granularity": "Daily",
                "aggregation": {
                    "totalCost": {
                        "function": "Sum",
                        "name": "PreTaxCost"
                    }
                }
            },
            "timeframe": "Custom",
            "timePeriod": {
                "from": "{{from.ToShortDateString()}}",
                "to": "{{to.ToShortDateString()}}"
            }
        }
        """;
        request.Method = HttpMethod.Post;
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<string> GetManagedIdentityToken()
    {
        var url = $"{Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT")}?api-version=2019-08-01&resource=https://management.azure.com";
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Add("X-IDENTITY-HEADER", Environment.GetEnvironmentVariable("IDENTITY_HEADER"));

        try
        {
            // request token
            var response = client.Send(request);
            var contentStream = await response.Content.ReadAsStreamAsync();

            // deserialize token from JSON
            Dictionary<string, string> tokenDict = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(contentStream);
            return tokenDict["access_token"];
        }
        catch (Exception ex)
        {
            string errorText = string.Format("{0} \n\n{1}", ex.Message, ex.InnerException != null ? ex.InnerException.Message : "Acquire token failed");
            throw new WebException(errorText, ex);
        }
    }
}