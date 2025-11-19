@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Name of the Key Vault to load secrets from. Required.')
param keyVaultName string

@description('Name of the subnet reserved for this App Service. Required.')
param subnetName string

@description('Name of the Virtual Network to place this app service within. Required.')
param virtualNetworkName string

@description('Client ID for the service principal deploying this script. Required.')
param servicePrincipalClientId string

@secure()
@description('Client secret for the service principal deploying this script. Required.')
param servicePrincipalClientSecret string

@description('Tenant ID for the service principal deploying this script. Required.')
param servicePrincipalTenantId string

resource deploymentScript 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: name
  location: location
  kind: 'AzureCLI'
  properties: {
    azCliVersion: '2.45.0'
    retentionInterval: 'PT1H'
    timeout: 'PT30M'
    environmentVariables: [
      {
        name: 'userName'
        value: servicePrincipalClientId
      }
      {
        name: 'password'
        secureValue: servicePrincipalClientSecret
      }
      {
        name: 'tenantId'
        value: servicePrincipalTenantId
      }
      {
        name: 'subscriptionId'
        value: subscription().subscriptionId
      }
      {
        name: 'keyVaultName'
        value: keyVaultName
      }
      {
        name: 'vnetName'
        value: virtualNetworkName
      }
      {
        name: 'subnetName'
        value: subnetName
      }
    ]
    scriptContent: '''
    az login --service-principal -u $userName -p $password --tenant $tenantId --allow-no-subscriptions
    az account set --subscription $subscriptionId

    az keyvault network-rule add --name $keyVaultName --vnet-name $vnetName --subnet $subnetName
    '''
  }
}
