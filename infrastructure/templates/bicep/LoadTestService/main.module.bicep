@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Name of the Key Vault to store the connection string into. Required.')
param keyVaultName string

resource loadTestService 'Microsoft.LoadTestService/loadTests@2022-12-01' = {
  name: name
  location: location
  identity: {
    type: 'SystemAssigned'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource keyVaultPolicies 'Microsoft.KeyVault/vaults/accessPolicies@2022-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        objectId: loadTestService.identity.principalId
        tenantId: loadTestService.identity.tenantId
        permissions: {
          certificates: [
            'get'
            'list'
          ]
          keys: [
            'decrypt'
            'encrypt'
            'get'
            'list'
            'sign'
            'verify'
          ]
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

output loadTestServiceDataPlaneURI string = loadTestService.properties.dataPlaneURI
output resourceId string = loadTestService.id
