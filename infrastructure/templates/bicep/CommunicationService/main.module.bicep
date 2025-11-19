@description('Unique name for this resource. Required.')
param name string

@description('Geography to store call data within. Required.')
@metadata({ url: 'https://learn.microsoft.com/en-us/azure/communication-services/concepts/privacy#data-residency' })
param dataLocation string

@description('Name of the Key Vault to store the connection string into. Required.')
param keyVaultName string

@description('Name of the App Configuration to store the resource URL into. Required.')
param appConfigName string

@description('Name of the Log Analytics Workspace to upload diagnostic logs to. Required.')
param logAnalyticsName string

@description('Unique release number for this deployment. Defaults to the current date.')
param releaseNumber string = utcNow('yyyyMMdd.HHmm')

@description('The ACS Provider managed identity details. Required.')
param acsProviderManagedIdentity object = {
  Name: ''
  ResourceGroup: ''
}

@description('The Queuing Service managed identity details. Required.')
param queuingServiceManagedIdentity object = {
  Name: ''
  ResourceGroup: ''
}

@description('The current UTC time.')
param currentUtcTime string = utcNow()

resource acsProviderMI 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: acsProviderManagedIdentity.Name
  scope: resourceGroup(acsProviderManagedIdentity.ResourceGroup)
}

resource queuingServiceMI 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: queuingServiceManagedIdentity.Name
  scope: resourceGroup(queuingServiceManagedIdentity.ResourceGroup)
}

resource existingCommunicationService 'Microsoft.Communication/communicationServices@2023-06-01-preview' existing = {
  name: name
}

resource communicationServices 'Microsoft.Communication/communicationServices@2023-06-01-preview' = if (existingCommunicationService.name == null) {
  name: name
  location: 'global'
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${acsProviderMI.id}': {}
      '${queuingServiceMI.id}': {}
    }
  }
  properties: {
    dataLocation: dataLocation
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

module connectionStringSecret1 '../KeyVault/secret.module.bicep' = {
  name: 'acsConnectionStringDeploy1-${releaseNumber}'
  dependsOn: [
    keyVault
  ]
  params: {
    key: 'ACS:ConnectionString'
    keyVaultName: keyVaultName
    value: communicationServices.listKeys().primaryConnectionString
    contentType: 'application/vnd.bag-StrongEncConnectionString'
    exp: dateTimeToEpoch(dateTimeAdd(currentUtcTime, 'P730D'))
  }
}

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: appConfigName
}

resource values 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'ACS:Url'
  parent: appConfig
  properties: {
    value: 'https://${communicationServices.properties.hostName}/'
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsName
}

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${name}-diagnosticSettings'
  scope: communicationServices
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

output resourceId string = communicationServices.id
