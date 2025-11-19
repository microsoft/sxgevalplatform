@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Front Door Resource Id. Required.')
param frontDoorResourceId string

@description('Environment into which to deploy resources. Required.')
param environment string

param version string

param serviceName string

resource frontDoorAlertDropInOriginHealthPercentage90 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} FrontDoor ${version} - Drop in Origin Health Percentage - 90 Percent'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Availability'
  }
  properties: {
    description: 'Front Door Drop in Origin Health Percentage'
    severity: environment == 'prod' ? 3 : 4
    enabled: true
    scopes: [
      frontDoorResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 90
          name: 'Metric1'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'OriginHealthPercentage'
          operator: 'LessThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: environment != 'prod'
    targetResourceType: 'Microsoft.Cdn/profiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource frontDoorAlertDropInOriginHealthPercentage50 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} FrontDoor ${version} - Drop in Origin Health Percentage - 50 Percent'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Availability'
  }
  properties: {
    description: 'Front Door Drop in Origin Health Percentage'
    severity: environment == 'prod' ? 2 : 4
    enabled: true
    scopes: [
      frontDoorResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 50
          name: 'Metric1'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'OriginHealthPercentage'
          operator: 'LessThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: environment != 'prod'
    targetResourceType: 'Microsoft.Cdn/profiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource frontDoorAlertHTTP5xxErrorsMedium 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} FrontDoor ${version} - HTTP 5xx Errors - Medium Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Front Door Increase in Percentage of HTTP 5xx Errors'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      frontDoorResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT30M'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Medium'
          failingPeriods: {
            numberOfEvaluationPeriods: 2
            minFailingPeriodsToAlert: 2
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'Percentage5XX'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Cdn/profiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource frontDoorAlertHTTP5xxErrorsLow 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} FrontDoor ${version} - HTTP 5xx Errors - Low Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Front Door Increase in Percentage of HTTP 5xx Errors'
    severity: environment == 'prod' ? 3 : 4
    enabled: environment == 'prod'
    scopes: [
      frontDoorResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT30M'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Low'
          failingPeriods: {
            numberOfEvaluationPeriods: 2
            minFailingPeriodsToAlert: 2
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'Percentage5XX'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: environment != 'prod'
    targetResourceType: 'Microsoft.Cdn/profiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource frontDoorAlertHTTP4xxErrorsLow 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} FrontDoor ${version} - HTTP 4xx Errors - Low Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Front Door Increase in Percentage of HTTP 4xx Errors'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      frontDoorResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT30M'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Low'
          failingPeriods: {
            numberOfEvaluationPeriods: 2
            minFailingPeriodsToAlert: 2
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'Percentage4XX'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Cdn/profiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource frontDoorAlertIncreaseInTotalLatencySev4 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} FrontDoor ${version} - Increase in Total Latency - Sev4'
  location: 'global'
  tags: {
    MonitoringCategory: 'Performance Monitoring'
    MonitoringSubCategory: 'Service Latency'
  }
  properties: {
    description: 'Front Door Increase in Total Latency'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      frontDoorResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT30M'
    criteria: {
      allOf: [
        {
          threshold: 3000
          name: 'Metric1'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'TotalLatency'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Cdn/profiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource frontDoorAlertIncreaseInTotalLatencySev3 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} FrontDoor ${version} - Increase in Total Latency - Sev3'
  location: 'global'
  tags: {
    MonitoringCategory: 'Performance Monitoring'
    MonitoringSubCategory: 'Service Latency'
  }
  properties: {
    description: 'Front Door Increase in Total Latency'
    severity: environment == 'prod' ? 3 : 4
    enabled: environment == 'prod'
    scopes: [
      frontDoorResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT30M'
    criteria: {
      allOf: [
        {
          threshold: 4500
          name: 'Metric1'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'TotalLatency'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: environment != 'prod'
    targetResourceType: 'Microsoft.Cdn/profiles'
    targetResourceRegion: 'global'
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}
