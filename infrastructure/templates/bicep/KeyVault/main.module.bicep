@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Name of the managed identity app services will run under. Used to grant those services access. Required.')
param managedIdentityName string

@description('Name of the Log Analytics Workspace to upload diagnostic logs to. Required.')
param logAnalyticsName string

@description('Unique release number for this deployment. Defaults to the current date.')
param releaseNumber string = utcNow('yyyyMMdd.HHmm')

@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: managedIdentityName
}

resource keyVaultResource 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    sku: {
      name: 'standard'
      family: 'A'
    }
    tenantId: tenant().tenantId
    createMode: 'default'
    enabledForDeployment: true
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    enableRbacAuthorization: false
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
    accessPolicies: [
      {
        tenantId: managedIdentity.properties.tenantId
        objectId: managedIdentity.properties.principalId
        permissions: {
          certificates: [
          ]
          keys: [
          ]
          secrets: [
            'get'
            'list'
          ]
        }
      }
      {
        tenantId: tenant().tenantId
        objectId: environment == 'int' ? 'f8daea97-62e7-4026-becf-13c2ea98e8b4' : 'b453993d-81d4-41a7-be3a-549bc2435ffa' // Microsoft Azure App Service
        permissions: {
          certificates: [
            'get'
          ]
          keys: [
          ]
          secrets: [
            'get'
          ]
        }
      }
      {
        tenantId: tenant().tenantId
        objectId: environment == 'int' ? 'ed47c2a1-bd23-4341-b39c-f4fd69138dd3' : 'c9eeec44-4409-4f15-a21a-f99466bc9695' // Microsoft.Azure.CertificateRegistration
        permissions: {
          certificates: [
            'get'
            'list'
          ]
          keys: [
          ]
          secrets: [
            'get'
            'list'
            'delete'
          ]
        }
      }
    ]
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsName
}

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${name}-diagnosticSettings'
  scope: keyVaultResource
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

// Alerts
module alerts 'alerts.module.bicep' = {
  name: 'keyVaultAlertsDeploy-${releaseNumber}'
  dependsOn: []
  params: {
    location: location
    actionGroupId: actionGroupId
    keyVaultResourceId: keyVaultResource.id
	  environment: environment
    serviceName: serviceName
  }
}

output keyVaultResourceId string = keyVaultResource.id
