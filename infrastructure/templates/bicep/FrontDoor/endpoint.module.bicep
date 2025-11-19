@description('Name of the front door to add this endpoint to. Required.')
param frontDoorName string

@description('Name of the route for this endpoint. Required.')
param routeName string

@description('Name of the endpoint. Required.')
param endpointName string

@description('Name of the origin group. Required.')
param originGroupName string

@description('Names of app services to route traffic to. Required.')
param appServiceNames array

@description('Name of the primary app service to route requests to. If not set, requests will be routed to the closest region. Optional.')
param primaryAppServiceName string = ''

resource frontDoorResource 'Microsoft.Cdn/profiles@2021-06-01' existing = {
  name: frontDoorName
}

// Controls how requests are routed to backend services (equivalent to BackendPools in classic Front Door)
resource frontDoorOriginGroup 'Microsoft.Cdn/profiles/originGroups@2021-06-01' = {
  name: originGroupName
  parent: frontDoorResource
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 2
      additionalLatencyInMilliseconds: 0
    }
    healthProbeSettings: {
      probePath: '/healthz'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 90
    }
    sessionAffinityState: 'Disabled'
  }
}

// Add an entry for our web server
resource appServiceOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2021-06-01' = [for appServiceName in appServiceNames: {
  name: appServiceName
  parent: frontDoorOriginGroup
  properties: {
    hostName: '${appServiceName}.azurewebsites.net'
    httpPort: 80
    httpsPort: 443
    originHostHeader: '${appServiceName}.azurewebsites.net'
    priority: empty(primaryAppServiceName) || primaryAppServiceName == appServiceName ? 1 : 2
    weight: 50
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
  }
}]

// Allocates a default domain name for our Front Door endpoint.
// Equivalent to Frontends/Domains in classic Front Door.
resource frontDoorEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2021-06-01' = {
  name: endpointName
  parent: frontDoorResource
  location: 'global'
  properties: {
    enabledState: 'Enabled'
  }
}

// Setup routing for the default domain to our backends
// Equivalent to Routing Rules in classic Front Door.
resource frontDoorDefaultRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2021-06-01' = {
  name: routeName
  parent: frontDoorEndpoint
  properties: {
    // No custom domain here since this configures the default generated domain
    customDomains: []
    linkToDefaultDomain: 'Enabled'
    // Route all requests to our default backend group
    originGroup: {
      id: frontDoorOriginGroup.id
    }
    ruleSets: []
    // Only support HTTPS since this is an API
    supportedProtocols: [
      'Https'
    ]
    patternsToMatch: [
      // Match all paths
      '/*'
    ]
    // Encrypt all traffic to our backends
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    enabledState: 'Enabled'
    cacheConfiguration: {
      queryStringCachingBehavior: 'UseQueryString'
    }
  }
}

output frontDoorHostName string = frontDoorEndpoint.properties.hostName
