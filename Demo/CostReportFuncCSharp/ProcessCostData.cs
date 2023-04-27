using Microsoft.Azure.WebJobs;

namespace PipeHow.CostReportFuncCSharp;

public static class ProcessCostData
{
    [FunctionName("ProcessCostData")]
    [return: Table("%CostTableName%")]
    public static CostDataEntry Run(
        [QueueTrigger("%CostQueueName%")] CostDataResponse entry)
    {
        var usageDate = entry.Properties.Rows[entry.Properties.Columns.FindIndex(c => c.Name == "UsageDate")];
        var currency = entry.Properties.Rows[entry.Properties.Columns.FindIndex(c => c.Name == "Currency")];
        var cost = double.Parse(entry.Properties.Rows[entry.Properties.Columns.FindIndex(c => c.Name == "PreTaxCost")]);

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