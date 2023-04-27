using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace PipeHow.CostReportFuncCSharp;

public static class GetCostData
{
    private static readonly HttpClient client = new();

    [FunctionName("GetCostData")]
    [return: Queue("%CostQueueName%")]
    public static async Task<CostDataResponse> Run(
        [TimerTrigger("0 0 6 * * *")] TimerInfo timer) // Run daily at 6AM
    {
        // Get access token
        var token = await GetManagedIdentityToken();
        // Get and post cost data to storage queue by returning it
        return await GetCostManagementData(timer.ScheduleStatus.Last, token);
    }

    public static async Task<CostDataResponse> GetCostManagementData(DateTime lastRun, string token)
    {
        // Assemble URL for Cost Management API
        string scope = Environment.GetEnvironmentVariable("CostScope");
        string url = $"https://management.azure.com/{scope}/providers/Microsoft.CostManagement/query?api-version=2022-10-01";
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Add("Content-Type", "application/json");
        request.Headers.Add("Authorization", $"Bearer {token}");

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
                "from": "{{lastRun.AddDays(-1).ToShortDateString()}}",
                "to": "{{lastRun.ToShortDateString()}}"
            }
        }
        """;
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
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