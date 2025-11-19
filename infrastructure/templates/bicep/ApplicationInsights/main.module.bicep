@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('Component Id for the service.Required.')
param componentId string

@description('Service Name.Required.')
param serviceName string

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 30
  }
  tags: {
    ComponentId: componentId
    Env: environment
  }
}

output resourceId string = appInsights.id
