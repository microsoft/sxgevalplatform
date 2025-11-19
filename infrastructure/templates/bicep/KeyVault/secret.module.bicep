@description('The name of the key vault to store this secret within. Required.')
param keyVaultName string

@description('The key of the secret. Colons in the name will automatically be escaped for Key Vault. Required.')
param key string

@secure()
@description('The value of the secret. Required.')
param value string

@description('The mime type of the secret value.')
param contentType string

@description('The expiration time of the secret in Unix time. Defaults to 4073677899.')
param exp int

// Convert hierarchical keys into a format Key Vault accepts.
// Key Vault will automatically convert back when the secret is loaded.
var escapedSecretKey = replace(key, ':', '--')

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource keyVaultSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  name: escapedSecretKey
  parent: keyVault
  properties: {
    value: value
    contentType: contentType
    attributes: {
      enabled: true
      exp: exp
    }
  }
}
