param trafficManagerName string
param trafficManagerLocation string
param routingMethod string
param trafficPath string
param actionGroupId string
param environment string

resource trafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' = {
  name: trafficManagerName
  location: trafficManagerLocation
  properties: {
    profileStatus: 'Enabled'
    trafficRoutingMethod: routingMethod
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

// Alerts
module alerts 'alerts.module.bicep' = {
  name: 'trafficManagerAlertsDeploy'
  dependsOn: [
    trafficManager
  ]
  params: {
    actionGroupId: actionGroupId
    trafficManagerProfileId: trafficManager.id
    environment: environment
    serviceName: trafficManagerName
    version: 'V1'
  }
}

output trafficManagerid string = trafficManager.id
output trafficManagerName string = trafficManager.name
