@description('The name of the function apps.')
param functionName string = 'costapp-${uniqueString(resourceGroup().id)}'

@description('Name of storage account (cannot contain any other characters than lowercase a-z)')
@maxLength(24)
param storageAccountName string = 'coststg${uniqueString(functionName)}'

@description('Name of app service plan.')
param appServicePlanName string = '${functionName}-asp'

@description('The location for the deployed resources.')
param location string = resourceGroup().location

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value}'
var pwshAppName = '${functionName}-pwsh'
var csharpAppName = '${functionName}-csharp'
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
]

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
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

resource storageFileService 'Microsoft.Storage/storageAccounts/fileServices@2022-09-01' = {
  parent: storageAccount
  name: 'default'
}

resource storageTableService 'Microsoft.Storage/storageAccounts/tableServices@2022-09-01' = {
  parent: storageAccount
  name: 'default'
}

resource storageTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  parent: storageTableService
  name: 'CostData'
}

module functionAppPwsh 'FuncApp.bicep' = {
  name: pwshAppName
  params: {
    location: location
    appServicePlanId: appServicePlan.id
    functionName: pwshAppName
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

module functionAppCSharp 'FuncApp.bicep' = {
  name: csharpAppName
  params: {
    location: location
    appServicePlanId: appServicePlan.id
    functionName: csharpAppName
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
      ])
    }
  }
}
