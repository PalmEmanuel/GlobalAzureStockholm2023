@description('The name of the function app.')
param functionName string

@description('The location for the deployed resources.')
param location string = resourceGroup().location

@description('The id of the app service plan for the function app.')
param appServicePlanId string

@description('The site config object of the function app.')
param siteConfig object

resource functionAppPwsh 'Microsoft.Web/sites@2022-09-01' = {
  name: functionName
  location: location
  kind: 'functionapp'
  properties: {
    dailyMemoryTimeQuota: 0
    serverFarmId: appServicePlanId
    siteConfig: siteConfig
  }
}
