@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Name of the Network Security Group to apply to all subnets. Required.')
param networkSecurityGroupName string

@description('Name of the subnet to reserve for App Service. Required.')
param appServiceSubnetName string

@description('Name of the subnet to reserve for Mock App Service. Required.')
param appServiceMockSubnetName string

@description('Unique release number for this deployment. Defaults to the current date.')
param releaseNumber string = utcNow('yyyyMMdd.HHmm')

@description('Alert Action Group ID. Required.')
param actionGroupId string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('Azure region abbreviation used to differentiate deployments.')
param regionAbbreviation string

@description('Name of the Public IP address for NAT. Required.')
param publicIpName string

@description('Name of the NAT Gateway. Required.')
param natGatewayName string

param serviceName string

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2022-09-01' = {
  name: networkSecurityGroupName
  location: location
  properties: {
    securityRules: []
  }
}

// Create a public IP address for the NAT gateway​
resource publicIp 'Microsoft.Network/publicIPAddresses@2023-09-01' = {
  name: publicIpName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
  }
}

// Create a NAT gateway​
resource natGateway 'Microsoft.Network/natGateways@2023-09-01' = {
  name: natGatewayName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIpAddresses:[
      {
        id: publicIp.id
      }
    ]
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2022-09-01' = {
  name: name
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.215.0.0/16'
      ]
    }
    subnets: [
      {
        name: appServiceSubnetName
        properties: {
          addressPrefix: '10.215.0.0/24'
          defaultoutboundaccess: false
          networkSecurityGroup: {
            id: networkSecurityGroup.id
          }
          //Associate subnet with NAT Gateway
          natGateway: {
            id: natGateway.id
          }
          // List services that our app service talks to, so communication can be routed through the vnet
          // https://learn.microsoft.com/en-us/azure/virtual-network/virtual-network-service-endpoints-overview
          serviceEndpoints: [
            {
              service: 'Microsoft.KeyVault'
              locations: [
                '*'
              ]
            }
            {
              service: 'Microsoft.ServiceBus'
              locations: [
                '*'
              ]
            }
            {
              service: 'Microsoft.Storage'
              locations: [
                'eastus'
                'westus'
              ]
            }
          ]
          // Host the VMs App Service allocates inside this subnet
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverfarms'
              }
            }
          ]
        }
      }
      {
        name: appServiceMockSubnetName
        properties: {
          addressPrefix: '10.215.1.0/24'
          defaultoutboundaccess: false
          networkSecurityGroup: {
            id: networkSecurityGroup.id
          }
          // List services that our app service talks to, so communication can be routed through the vnet
          // https://learn.microsoft.com/en-us/azure/virtual-network/virtual-network-service-endpoints-overview
          serviceEndpoints: [
            {
              service: 'Microsoft.KeyVault'
              locations: [
                '*'
              ]
            }
            {
              service: 'Microsoft.ServiceBus'
              locations: [
                '*'
              ]
            }
            {
              service: 'Microsoft.Storage'
              locations: [
                'eastus'
                'westus'
              ]
            }
          ]
          // Host the VMs App Service allocates inside this subnet
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverfarms'
              }
            }
          ]
        }
      }
    ]
  }
}

// Alerts
module alerts 'alerts.module.bicep' = {
  name: 'vNetAlertsDeploy-${regionAbbreviation}-${releaseNumber}'
  dependsOn: []
  params: {
    location: location
    actionGroupId: actionGroupId
    virtualNetworkResourceId: virtualNetwork.id
	  environment: environment
    serviceName: serviceName
  }
}

output vnetResourceId string = virtualNetwork.id
