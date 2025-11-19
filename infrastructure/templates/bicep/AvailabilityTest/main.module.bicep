@description('Unique name for the Alert Action Group. Required.')
param name string

@description('Action Group ID. Required.')
param actionGroupId string

@description('Azure region to deploy the resource. Required.')
param resourceGroupLocation string

@description('Name of the Application Insights. Required.')
param appInsightsName string

@description('Name of the hostName used for the availability test. Required.')
param hostName string

@description('Name of the AppService used for the availability test. Required.')
param appServiceName string

param serviceName string

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: name
  location: resourceGroupLocation
  kind: 'standard'
  tags: {
    'hidden-link:${appInsights.id}': 'Resource'
  }
  properties: {
    Enabled: true
    Frequency: 300
    Timeout: 120
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      {
        Id: 'us-va-ash-azr' //East US
      }
      {
        Id: 'us-ca-sjc-azr' //West US
      }
      {
        Id: 'emea-nl-ams-azr' //West Europe
      }
      {
        Id: 'apac-sg-sin-azr' //Southeast Asia
      }
    ]
    Request: {
      RequestUrl: 'https://${hostName}/healthz'
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
    Name: name
    SyntheticMonitorId: name 
  } 
}

resource pingAlertRule 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - Health Check ${appServiceName}'
  location: 'global'
  tags: {
		MonitoringCategory: 'Reliability & Availability'
		MonitoringSubCategory: 'Availability'
	}
  properties: {
    actions: [
      {
        actionGroupId: actionGroupId
      }
    ]
    description: 'Alert for a web test for ${appServiceName}'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: availabilityTest.id
      componentId: appInsights.id
      failedLocationCount: 2
    }
    enabled: true
    evaluationFrequency: 'PT5M' 
    scopes: [
      availabilityTest.id
      appInsights.id
    ]
    severity: 4
    windowSize: 'PT15M'
  }
}
