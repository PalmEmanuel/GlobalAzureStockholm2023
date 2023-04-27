using System;
using System.Collections.Generic;
using Azure;
using Azure.Data.Tables;

namespace PipeHow.CostReportFuncCSharp;

public class CostDataResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Location { get; set; }
    public string SKU { get; set; }
    public string ETag { get; set; }
    public CostDataProperties Properties { get; set; }
}

public class CostDataProperties
{
    public string NextLink { get; set; }
    public List<CostDataColumn> Columns { get; set; }
    public List<string> Rows { get; set; }
}

public class CostDataColumn
{
    public string Name { get; set; }
    public string Type { get; set; }
}

public class CostDataEntry : ITableEntity
{
    public double PreTaxCost { get; set; }
    public string UsageDate { get; set; }
    public string Currency { get; set; }

    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}