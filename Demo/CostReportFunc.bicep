@description('The base app name for the resources.')
@maxLength(20)
param appName string = 'costapp${uniqueString(resourceGroup().id)}'

@description('Name of storage account (cannot contain any other characters than lowercase a-z)')
@maxLength(24)
param storageAccountName string = '${appName}stg'

// Y1 is consumption, but it's too cheap for the demo
@description('SKU of app service plan.')
param appServicePlanSKU string = 'S1'

@description('Name of app service plan.')
param appServicePlanName string = '${appName}-asp'

@description('Name of app insights.')
param appInsightsName string = '${appName}-appin'

@description('Name of log analytics workspace.')
param logAnalyticsName string = '${appName}-log'

@description('The name of the Azure Storage Table.')
param tableName string = 'CostData'

@description('The name of the Azure Storage Queue.')
param queueName string = 'CostDataToProcess'

@description('The location for the deployed resources.')
param location string = resourceGroup().location

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value}'
var pwshAppName = '${appName}-func-pwsh'
var csharpAppName = '${appName}-func-csharp'
var commonAppSettings = [
  {
    name: 'AzureWebJobsStorage'
    value: storageConnectionString
  }
  {
    name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING' // Required for Consumption
    value: storageConnectionString
  }
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_APP_EDIT_MODE'
    value: 'readonly'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING '
    value: 'InstrumentationKey=${appInsights.properties.InstrumentationKey}'
  }
  {
    name: 'ApplicationInsightsAgent_EXTENSION_VERSION '
    value: '~2'
  }
  {
    name: 'CostTableName'
    value: tableName
  }
  {
    name: 'CostQueueName'
    value: queueName
  }
]

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSKU
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  location: location
  name: logAnalyticsName
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    WorkspaceResourceId: logAnalyticsWorkspace.id
    Application_Type: 'web'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    encryption: {
      services: {
        blob: {
          enabled: true
          keyType: 'Account'
        }
        file: {
          enabled: true
          keyType: 'Account'
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

resource storageQueueService 'Microsoft.Storage/storageAccounts/queueServices@2022-09-01' = {
  parent: storageAccount
  name: 'default'
}

resource storageTableService 'Microsoft.Storage/storageAccounts/tableServices@2022-09-01' = {
  parent: storageAccount
  name: 'default'
}

resource storageQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2022-09-01' = {
  parent: storageQueueService
  name: queueName
}

resource storageTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  parent: storageTableService
  name: tableName
}

resource costReaderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: '72fafb9e-0641-4937-9268-a91bfd8191a3' // Cost Management Reader
    principalId: functionAppCSharp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource functionAppCSharp 'Microsoft.Web/sites@2022-09-01' = {
  name: csharpAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dailyMemoryTimeQuota: 0
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v7.0'
      appSettings: concat(commonAppSettings, [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'WEBSITE_CONTENTSHARE' // Required for Consumption
          value: csharpAppName
        }
        {
          name: 'CostScope'
          value: 'subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroup().name}'
        }
      ])
    }
  }
}

resource functionAppPwsh 'Microsoft.Web/sites@2022-09-01' = {
  name: pwshAppName
  location: location
  kind: 'functionapp'
  properties: {
    dailyMemoryTimeQuota: 0
    serverFarmId: appServicePlan.id
    siteConfig: {
      powerShellVersion: '7.2'
      appSettings: concat(commonAppSettings, [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'powershell'
        }
        {
          name: 'WEBSITE_CONTENTSHARE' // Required for Consumption
          value: pwshAppName
        }
      ])
    }
  }
}
