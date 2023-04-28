using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace PipeHow.CostReportFuncCSharp;

public static class ProcessCostData
{
    [FunctionName("ProcessCostData")]
    [return: Table("%CostTableName%")]
    public static CostDataEntry Run(
        [QueueTrigger("%CostQueueName%")] CostDataResponse entry, ILogger log)
    {
        try
        {
            var usageDate = entry.Properties.Rows[0][entry.Properties.Columns.FindIndex(c => c.Name == "UsageDate")];
            var cost = double.Parse(entry.Properties.Rows[0][entry.Properties.Columns.FindIndex(c => c.Name == "PreTaxCost")]);
            var currency = entry.Properties.Rows[0][entry.Properties.Columns.FindIndex(c => c.Name == "Currency")];
            log.LogInformation($"Date: {usageDate}, Cost: {cost}, Currency: {currency}");

            return new CostDataEntry
            {
                PartitionKey = "CostData",
                RowKey = usageDate,
                Currency = currency,
                PreTaxCost = cost,
                UsageDate = usageDate
            };
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not post entry to table!", entry);
            throw;
        }
    }
}