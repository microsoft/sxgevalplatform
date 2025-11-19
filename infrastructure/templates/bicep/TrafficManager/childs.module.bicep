param childTrafficManagerObj object
param parentTrafficManagerName string
param childTrafficManagerLocation string
param routingMethod string
param trafficPath string
param childEndpointCountToHealthCheck int = 1

resource childTrafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' = {
  name: childTrafficManagerObj.tmName
  location: childTrafficManagerLocation
  properties: {
    profileStatus: 'Enabled'
    trafficRoutingMethod: routingMethod
    minChildEndpoints: childEndpointCountToHealthCheck
    dnsConfig: {
      relativeName: childTrafficManagerObj.tmName
      ttl: 60
    }
    monitorConfig: {
      protocol: 'HTTPS'
      port: 443
      path: trafficPath
      expectedStatusCodeRanges: [
        {
          min: 200
          max: 299
        }
      ]
      intervalInSeconds: 30 
      toleratedNumberOfFailures: 3 
      timeoutInSeconds: 10 
    }
  }
}

// Existing Traffic Manager
resource parentTrafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' existing = {
  name: parentTrafficManagerName
}

resource nestedEndpoint 'Microsoft.Network/trafficmanagerprofiles/NestedEndpoints@2022-04-01' = {
  name: '${childTrafficManager.name}-endpoint'
  parent: parentTrafficManager
  properties: {
    targetResourceId: childTrafficManager.id
    endpointStatus: 'Enabled'
    endpointLocation: childTrafficManagerObj.tmLocation
  }
}
