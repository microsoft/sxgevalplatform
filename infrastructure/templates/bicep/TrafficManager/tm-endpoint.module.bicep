param serviceTrafficManagerName string
param regionalTrafficManagerName string
param targetResourcePriority int

resource serviceTrafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' existing = {
  name: regionalTrafficManagerName
}

resource regionalTrafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' existing = {
  name: serviceTrafficManagerName
}

resource backupEndpoint 'Microsoft.Network/trafficmanagerprofiles/NestedEndpoints@2022-04-01' = {
  name: '${regionalTrafficManagerName}-endpoint'
  parent: regionalTrafficManager
  properties: {
    targetResourceId: serviceTrafficManager.id
    endpointStatus: 'Enabled'
    minChildEndpoints: 1
    priority: targetResourcePriority
  }
}
