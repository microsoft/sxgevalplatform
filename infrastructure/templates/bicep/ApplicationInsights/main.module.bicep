@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Resource identifier of the log analytics workspace to bind app insights to. Required.')
param workspaceResourceId string

@description('Unique release number for this deployment. Defaults to the current date.')
param releaseNumber string = utcNow('yyyyMMdd.HHmm')

@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    //WorkspaceResourceId: workspaceResourceId
    RetentionInDays: 30
  }
}

// Alerts
// module alerts 'alerts.module.bicep' = {
//   name: 'appInsightsAlertsDeploy-${releaseNumber}'
//   dependsOn: []
//   params: {
//     location: location
//     actionGroupId: actionGroupId
//     appInsightsResourceId: appInsights.id
// 	  environment: environment
//     serviceName: serviceName
//   }
// }

output resourceId string = appInsights.id
