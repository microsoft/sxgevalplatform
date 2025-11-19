param trafficManagerName string
param trafficManagerLocation string
param routingMethod string
param trafficPath string
param parentEndpointCountToHealthCheck int = 1
param childEndpoints array
param actionGroupId string
param environment string

resource parentTrafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' = {
  name: trafficManagerName
  location: trafficManagerLocation
  properties: {
    profileStatus: 'Enabled'
    trafficRoutingMethod: routingMethod
    minChildEndpoints: parentEndpointCountToHealthCheck
    dnsConfig: {
      relativeName: trafficManagerName
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

module childTrafficManagerEndpoints 'childs.module.bicep' = [for (childEndpoint, i) in childEndpoints: {
  name: 'childTMEndpointDeploy-${i}'
  params: {
    childTrafficManagerObj: childEndpoint
    parentTrafficManagerName: trafficManagerName
    childTrafficManagerLocation: 'global'
    routingMethod: 'Priority'
    trafficPath: '/healthz'
  }
}]

module alerts 'alerts.module.bicep' = {
  name: 'trafficManagerAlertsDeploy'
  dependsOn: [
    parentTrafficManager
  ]
  params: {
    actionGroupId: actionGroupId
    trafficManagerProfileId: parentTrafficManager.id
    environment: environment
    serviceName: trafficManagerName
    version: 'V1'
  }
}
output trafficManagerid string = parentTrafficManager.id
output trafficManagerName string = parentTrafficManager.name
