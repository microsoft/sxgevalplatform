// Deploy Shared Infrastructure
// This script is responsible for deploying resources that are shared between all regions.
@allowed([
  'dev'
  'ppe'
  'prod'
])
@description('Environment into which to deploy resources. Required.')
param environment string

// @description('Azure regions this app will be deployed in. Used by databases and other resources that support replicas. Required.')
// param regions array

@description('Azure region in which to deploy shared resources which are only deployed in one region.')
param location string = resourceGroup().location

@description('ACS User Id to use for the application. You must create this ID in the ACS resource, then pass it into the pipeline. Defaults to empty to allow for initial setup of an environment.')
param applicationId string = ''

@description('User-Assigned Managed Identity Client Id. Required.')
param azureClientId string

@description('AzureAD tenant this app runs under. Required.')
param azureTenantId string

@description('App Registration Client Id. Required.')
param azureAdClientId string

@description('Unique release number for this deployment. Defaults to the current date.')
param releaseNumber string = utcNow('yyyyMMdd.HHmm')

@description('Component Id for the service.Required.')
param componentId string

@description('Service Name.Required.')
param serviceName string

@description('Prefix for the resource name.Required.')
param resourcePrefix string

// Get names for resources
// module resourceNames 'naming.bicep' = {
//   name: 'resourceNames-${releaseNumber}'
//   params: {
//     environment: environment
//     location: location
//     prefix: resourcePrefix
//     regions: regions
//   }
// }

module managedIdentity 'templates/bicep/ManagedIdentity/main.module.bicep' = {
  name: 'managedIdentity-${releaseNumber}'
  dependsOn: [
    //resourceNames
  ]
  params: {
    name: 'sxg-eval-managedIdentity-${environment}'
    location: location
    // serviceName: serviceName
    // environment: environment
    // componentId: componentId
  }
}

// App Insights
module appInsights 'templates/bicep/ApplicationInsights/main.module.bicep' = {
  name: 'appInsightsDeploy-${releaseNumber}'
  dependsOn: [
    //resourceNames
    // logAnalytics
    // keyVault
  ]
  params: {
    name: 'sxg-eval-appInsights-${environment}'
    location: location
    serviceName: serviceName
    environment: environment
    componentId: componentId
  }
}

// Log Analytics Workspace
module logAnalytics 'templates/bicep/LogAnalytics/main.module.bicep' = {
  name: 'logAnalyticsDeploy-${releaseNumber}'
  dependsOn: [
    //resourceNames
  ]
  params: {
    name: 'sxg-eval-logAnalytics-${environment}'
    location: location
  }
}

// Storage Account
module storageAccount 'templates/bicep/StorageAccount/main.module.bicep' = {
  name: 'storageAccountDeploy-${releaseNumber}'
  dependsOn: [
    // resourceNames
     logAnalytics
  ]
  params: {
    name: 'sxgevalstorageaccount${environment}'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    serviceName: serviceName
    environment: environment
    componentId: componentId
  }
}

// Service Bus
module serviceBus 'templates/bicep/ServiceBus/main.module.bicep' = {
  name: 'serviceBusDeploy-${releaseNumber}'
  dependsOn: [
    // resourceNames
  ]
  params: {
    name: 'sxg-eval-serviceBus-${environment}'
    location: location
    environment: environment
    componentId: componentId
    queueName: 'evalResults'
    storageAccountId: storageAccount.outputs.storageAccountId
  }
}