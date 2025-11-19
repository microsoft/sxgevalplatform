param appServiceName string

param subnetId string

param ipsFromIpGroups array

resource appService 'Microsoft.Web/sites@2022-03-01' existing = {
  name: appServiceName
}

var defaultIpRestrictions = [
  {
    ipAddress: 'ApplicationInsightsAvailability'
    action: 'Allow'
    tag: 'ServiceTag'
    priority: 100
    name: 'Allow Availability Tests'
  }
  {
    ipAddress: 'AzureFrontDoor.Backend'
    action: 'Allow'
    tag: 'ServiceTag'
    priority: 200
    name: 'Allow Azure Front Door'
  }
  {
    ipAddress: 'AzureTrafficManager'
    action: 'Allow'
    tag: 'ServiceTag'
    priority: 201
    name: 'Allow Azure Traffic Manager'
  }
  {
    ipAddress: 'AzureEventGrid'
    action: 'Allow'
    tag: 'ServiceTag'
    priority: 300
    name: 'Allow Azure Event Grid'
  }
  // Optional if you have multiple app services within the same vnet​
  {
    vnetSubnetResourceId: subnetId
    action: 'Allow'
    tag: 'Default'
    priority: 400
    name: 'Allow VNet'
  }
  // Final rule to deny all other traffic​
  {
    ipAddress: 'Any'
    action: 'Deny'
    priority: 40000
    name: 'Deny All'
  }
]

var flattenedIps = flatten(ipsFromIpGroups)

var allRestrictions = [for (ip,i) in flattenedIps: {
  ipAddress: ip.ipAddress
  action: 'Allow'
  priority: 500 + i
  name: ip.name
}]

var acsIps = split('52.112.0.0/14,52.122.0.0/15,52.238.119.141/32,52.244.160.207/32,2603:1027::/48,2603:1037::/48,2603:1047::/48,2603:1057::/48,2603:1063::/38,2620:1ec:6::/48,2620:1ec:40::/42', ',')
var acsRestrictions = [for (ip,i) in acsIps: {
  ipAddress: ip
  action: 'Allow'
  priority: 700 + i
  name: 'Allow-ACS-${i}'
}]

var aiAgentIps = split('4.189.241.22/32, 4.207.11.123/32, 4.144.145.3/32, 4.153.0.99/32, 4.246.18.26/32, 98.64.19.184/32, 52.167.190.148/32, 52.177.87.114/32, 52.184.238.224/32, 52.184.238.233/32, 52.242.111.244/32, 52.254.40.37/32, 20.49.97.13/32, 52.254.40.143/32, 52.254.41.17/32, 52.254.41.72/32, 52.254.41.174/32, 52.254.41.211/32, 52.254.41.220/32, 52.254.42.16/32, 52.254.42.19/32, 52.254.42.76/32, 52.254.42.150/32, 52.254.43.122/32, 52.254.43.192/32, 52.254.43.245/32, 52.254.44.81/32, 52.254.44.93/32, 52.254.44.148/32, 52.254.44.153/32, 52.254.44.176/32, 52.254.44.196/32, 52.254.44.217/32, 52.254.44.220/32, 52.254.44.245/32, 52.254.45.2/32, 52.254.45.16/32', ',')
var aiAgentRestrictions = [for (ip,i) in aiAgentIps: {
  ipAddress: trim(ip)
  action: 'Allow'
  priority: 900 + i
  name: 'Allow-AiBot-${i}'
}]

// Configure inbound restrictions for the App Service​
resource appServiceConfig 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: appService
  name: 'web'
  properties: {
    ipSecurityRestrictions: concat(defaultIpRestrictions, allRestrictions, acsRestrictions, aiAgentRestrictions)
    ipSecurityRestrictionsDefaultAction: 'Deny'
  }
}
