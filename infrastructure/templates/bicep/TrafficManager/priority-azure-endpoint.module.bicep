param trafficManagerName string
param appServiceName string
param appServiceId string
param priorityEndpoint int

// Existing Traffic Manager
resource trafficManager 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' existing = {
  name: trafficManagerName
}

// Traffic Manager Endpoints
resource azureEndpoint 'Microsoft.Network/trafficmanagerprofiles/AzureEndpoints@2022-04-01' = {
  name: '${appServiceName}-endpoint'
  parent: trafficManager
  properties: {
    targetResourceId: appServiceId
    endpointStatus: 'Enabled'
    priority: priorityEndpoint
  }
}
