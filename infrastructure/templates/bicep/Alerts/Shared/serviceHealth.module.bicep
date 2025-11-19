@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Name of the Application Insights. Required.')
param appInsightsId string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

param dependencyRequestDurationSev3Threshold int

param dependencyRequestDurationSev4Threshold int

resource serviceAlertDependencyRequestDurationSev4 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - Increase in Dependency Request Duration - Sev4'
  location: 'global'
  tags: {
    MonitoringCategory: 'Performance Monitoring'
    MonitoringSubCategory: 'Service Latency'
  }
  properties: {
    description: 'App Service Dependency Increase in Request Duration'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      appInsightsId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT1H'
    criteria: {
      allOf: [
        {
          threshold: dependencyRequestDurationSev4Threshold
          name: 'Metric1'
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/duration'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Insights/components'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource serviceAlertDependencyRequestDurationSev3 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - Increase in Dependency Request Duration - Sev3'
  location: 'global'
  tags: {
    MonitoringCategory: 'Performance Monitoring'
    MonitoringSubCategory: 'Service Latency'
  }
  properties: {
    description: 'App Service Dependency Increase in Request Duration'
    severity: environment == 'prod' ? 3 : 4
    enabled: environment == 'prod'
    scopes: [
      appInsightsId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT1H'
    criteria: {
      allOf: [
        {
          threshold: dependencyRequestDurationSev3Threshold
          name: 'Metric1'
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/duration'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: environment == 'prod' ? false : true
    targetResourceType: 'Microsoft.Insights/components'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource serviceAlertDependencyFailuresMedium 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - Increase in Dependency Failures - Medium Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Dependency'
    MonitoringSubCategory: 'Error Rates'
  }
  properties: {
    description: 'Abnormal number of Dependency Failures'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      appInsightsId
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
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/failed'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Insights/components'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}

resource serviceAlertDependencyFailuresLow 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - Increase in Dependency Failures - Low Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Dependency'
    MonitoringSubCategory: 'Error Rates'
  }
  properties: {
    description: 'Abnormal number of Dependency Failures'
    severity: environment == 'prod' ? 3 : 4
    enabled: environment == 'prod'
    scopes: [
      appInsightsId
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
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/failed'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: environment == 'prod' ? false : true
    targetResourceType: 'Microsoft.Insights/components'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}
