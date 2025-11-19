param trafficManagerName string
param targetResourceName string
param targetResourceId string
param targetResourceLocation string

// Existing Traffic Manager
resource trafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' existing = {
  name: trafficManagerName
}

resource azureEndpoint 'Microsoft.Network/trafficmanagerprofiles/NestedEndpoints@2022-04-01' = {
  name: '${targetResourceName}-endpoint'
  parent: trafficManager
  properties: {
    targetResourceId: targetResourceId
    endpointStatus: contains(tolower(targetResourceName), 'neu') ? 'Disabled' : 'Enabled'
    endpointLocation: targetResourceLocation
  }
}
