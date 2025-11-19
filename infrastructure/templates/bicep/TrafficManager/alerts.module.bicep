@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Traffic Manager Profile Resource Id. Required.')
param trafficManagerProfileId string

@description('Environment into which to deploy resources. Required.')
param environment string

param version string
param serviceName string

// Only health alerts param (optional, can be removed if not needed)
param onlyHealthAlerts bool = false

// Alert: ProbeAgentCurrentEndpointStateByProfileResourceID
resource tmAlertProbeAgentCurrentEndpointState 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} TrafficManager ${version} - Probe Agent Endpoint State'
  location: 'global'
  tags: {
    MonitoringCategory: 'Availability'
    MonitoringSubCategory: 'Probe Agent Endpoint State'
  }
  properties: {
    description: 'Traffic Manager Probe Agent Current Endpoint State is unhealthy'
    severity: 3
    enabled: environment == 'prod'
    scopes: [
      trafficManagerProfileId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 0
          name: 'Metric1'
          metricNamespace: 'Microsoft.Network/trafficManagerProfiles'
          metricName: 'ProbeAgentCurrentEndpointStateByProfileResourceID'
          operator: 'Equals'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Network/trafficManagerProfiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {}
      }
    ]
  }
}

// Alert: QpsByEndpoint
resource tmAlertQpsByEndpoint 'microsoft.insights/metricAlerts@2018-03-01' = if (!onlyHealthAlerts) {
  name: '${serviceName} TrafficManager ${version} - QPS By Endpoint'
  location: 'global'
  tags: {
    MonitoringCategory: 'Performance'
    MonitoringSubCategory: 'QPS'
  }
  properties: {
    description: 'Traffic Manager QPS By Endpoint is high'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      trafficManagerProfileId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 1000 // Adjust threshold as needed
          name: 'Metric1'
          metricNamespace: 'Microsoft.Network/trafficManagerProfiles'
          metricName: 'QpsByEndpoint'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Network/trafficManagerProfiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {}
      }
    ]
  }
}
