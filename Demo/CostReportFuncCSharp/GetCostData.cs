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
    private static ILogger logger;
    private static readonly HttpClient client = new();

    [FunctionName("GetCostData")]
    [return: Queue("%CostQueueName%")]
    public static async Task<CostDataResponse> Run(
        [TimerTrigger("0 0 6 * * *")] TimerInfo _, ILogger log) // Run daily at 6AM
    {
        logger = log;
        // Get access token
        var token = await GetManagedIdentityToken();
        logger.LogInformation(token);
        // Get and post cost data to storage queue by returning it
        return await GetCostManagementData(token);
    }

    public static async Task<CostDataResponse> GetCostManagementData(string token)
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
                "from": "{{from:yyyy-MM-dd}}",
                "to": "{{to:yyyy-MM-dd)}}"
            }
        }
        """;
        request.Method = HttpMethod.Post;
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        logger.LogInformation(await response.Content.ReadAsStringAsync());
        var contentStream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<CostDataResponse>(contentStream);
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