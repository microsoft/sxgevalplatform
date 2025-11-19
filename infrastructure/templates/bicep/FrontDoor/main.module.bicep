@description('Unique name for this resource. Required.')
param name string

@description('Names of app services to route traffic to. Required.')
param appServiceNames array

@description('Names of regions to deploy additional regional endpoints to. Optional.')
param regionalAbbreviations array = []

@description('Name of the default route. Required.')
param defaultRouteName string

@description('Name of the Log Analytics Workspace to upload diagnostic logs to. Required.')
param logAnalyticsName string

@description('Name of the Web Application Firewall Policy for this Front Door resource. Required.')
param wafPolicyName string

@description('Unique release number for this deployment. Defaults to the current date.')
param releaseNumber string = utcNow('yyyyMMdd.HHmm')

@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('Common Resource Group Name.')
param commonResourceGroup string

param serviceName string

// Main container for Azure Front Door configuration
resource frontDoorResource 'Microsoft.Cdn/profiles@2021-06-01' = {
  name: name
  location: 'global'
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  properties: {
    originResponseTimeoutSeconds: 60
  }
}

// Setup our endpoints
module defaultFrontDoorEndpoint 'endpoint.module.bicep' = {
  name: 'frontDoorEndpointDeploy-${releaseNumber}'
  params: {
    appServiceNames: appServiceNames
    endpointName: name
    frontDoorName: name
    originGroupName: name
    routeName: defaultRouteName
  }
}

module regionalFrontDoorEndpoints 'endpoint.module.bicep' = [for (regionAbbreviation, i) in regionalAbbreviations: {
  name: 'frontDoorEndpointDeploy-${regionAbbreviation}-${releaseNumber}'
  params: {
    appServiceNames: appServiceNames
    endpointName: '${name}-${regionAbbreviation}'
    frontDoorName: name
    originGroupName: '${name}-${regionAbbreviation}'
    routeName: '${name}-${regionAbbreviation}'
    primaryAppServiceName: appServiceNames[i]
  }
}]

// Get WAF policies from Common to link to FrontDoor
resource commonWafPolicy 'Microsoft.Network/frontdoorwebapplicationfirewallpolicies@2022-05-01' existing = {
  scope: resourceGroup(commonResourceGroup)
  name: 'ccapicommonwafpolicy${environment}'
}

// Attach it to each of our endpoints
var allEndpointNames = concat([name], map(regionalAbbreviations, regionAbbreviation => '${name}-${regionAbbreviation}'))
resource securityPolicy 'Microsoft.Cdn/profiles/securitypolicies@2020-09-01' = {
  name: wafPolicyName
  parent: frontDoorResource
  dependsOn: [
    defaultFrontDoorEndpoint
    regionalFrontDoorEndpoints
  ]
  properties: {
    parameters: {
      type: 'WebApplicationFirewall'
      wafPolicy: {
        id: commonWafPolicy.id
      }
      associations: [
        {
          domains: [for endpointName in allEndpointNames: {
              // Using resourceId here to work around Bicep limitations around arrays of module deployments
              id: resourceId('Microsoft.Cdn/profiles/afdEndpoints', name, endpointName)
            }
          ]
          patternsToMatch: [
            '/*'
          ]
        }
      ]
    }
  }
}

// Setup diagnostic logs
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsName
}

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${name}-diagnosticSettings'
  scope: frontDoorResource
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
  name: 'frontDoorAlertsDeploy-${releaseNumber}'
  dependsOn: []
  params: {
    actionGroupId: actionGroupId
    frontDoorResourceId: frontDoorResource.id
	  environment: environment
    serviceName: serviceName
    version: 'V1'
  }
}

output frontDoorResourceId string = frontDoorResource.id
output frontDoorHostName string = defaultFrontDoorEndpoint.outputs.frontDoorHostName
