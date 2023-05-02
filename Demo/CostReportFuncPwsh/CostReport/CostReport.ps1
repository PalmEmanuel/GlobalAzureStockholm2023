using namespace System.Net

param($Request, $CostTable, $TriggerMetadata)

Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $CostTable | Select-Object -Property @(
        'Currency',
        @{ Name = 'PreTaxCost'; Expression = { [decimal]$_.PreTaxCost } },
        @{ Name = 'UsageDate'; Expression = { [datetime]::ParseExact($_.UsageDate, 'yyyyMMdd', $null).ToString('yyyy-MM-dd') } }
    )
})