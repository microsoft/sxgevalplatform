@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Name of the Log Analytics Workspace to upload diagnostic logs to. Required.')
param logAnalyticsName string

@description('Unique release number for this deployment. Defaults to the current date.')
param releaseNumber string = utcNow('yyyyMMdd.HHmm')

@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('Name of the managed identity app services will run under. Used to grant those services access. Required.')
param managedIdentityName string

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: managedIdentityName
}

resource appConfiguration 'Microsoft.AppConfiguration/configurationStores@2024-05-01' = {
  name: name
  location: location
  sku: {
    name: 'standard'
  }
  properties: {
    createMode: 'Default'
    encryption: {}
    disableLocalAuth: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    dataPlaneProxy: {
      authenticationMode: 'Pass-through'
      privateLinkDelegation: 'Disabled'
    }
  }
} 

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsName
}

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${name}-diagnosticSettings'
  scope: appConfiguration
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

output resourceId string = appConfiguration.id
