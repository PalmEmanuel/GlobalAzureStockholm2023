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
        var usageDate = entry.Properties.Rows[entry.Properties.Columns.FindIndex(c => c.Name == "UsageDate")];
        var currency = entry.Properties.Rows[entry.Properties.Columns.FindIndex(c => c.Name == "Currency")];
        var cost = double.Parse(entry.Properties.Rows[entry.Properties.Columns.FindIndex(c => c.Name == "PreTaxCost")]);
        log.LogInformation(usageDate);
        log.LogInformation(currency);
        log.LogInformation(cost.ToString());

        return new CostDataEntry
        {
            PartitionKey = "CostData",
            RowKey = usageDate,
            Currency = currency,
            PreTaxCost = cost,
            UsageDate = usageDate
        };
    }
}