@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('Component Id for the service.Required.')
param componentId string

@description('Queue Name.Required.')
param queueName string

@description('skuName.Required.')
param skuName string = 'Standard' // Options: Basic, Standard, Premium

@description('storage account for diagnosticSetting.Required.')
param storageAccountId string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: name
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  tags: {
    ComponentId: componentId
    Env: environment
  }
  properties: {
    premiumMessagingPartitions: 0
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    zoneRedundant: true
  }
}

resource diagnosticSetting 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'ServiceBusDiagnostics'
  scope: serviceBusNamespace
  properties: {
    storageAccountId: storageAccountId
    logs: [
      {
        category: 'OperationalLogs'
        enabled: true
      }
      {
        category: 'RuntimeAuditLogs'
        enabled: true
      }
      {
        category: 'DiagnosticErrorLogs'
        enabled: true
      }
      {
        category: 'ApplicationMetricsLogs'
        enabled: true
      }
      {
        category: 'DataDRLogs'
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

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  name: '${name}/${queueName}'
  properties: {
    enablePartitioning: true
    maxSizeInMegabytes: 1024
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'P7D'
    requiresDuplicateDetection: false
  }
}
