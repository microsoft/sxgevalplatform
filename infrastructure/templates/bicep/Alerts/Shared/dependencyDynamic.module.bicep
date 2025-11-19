@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Id of the Application Insights. Required.')
param appInsightsId string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Dependency name to monitor. Required.')
param dependencyName string

@description('Dependency target to monitor. Required.')
param dependencyTargets array

@description('Dependency target type to monitor. Required.')
param dependencyTargetOperatorType string

param serviceName string

param failureAlertType string

resource dependencyFailureCountMedium 'microsoft.insights/metricAlerts@2018-03-01' = if (failureAlertType == 'Dynamic') {
	name: '${serviceName} - ${dependencyName} Failure Increase - Medium Threshold'
	location: 'global'
	tags: {
		MonitoringCategory: 'Partner Dependency'
		MonitoringSubCategory: 'Per Partner'
	}
	properties: {
		description: 'Dependency Failure Increase ${dependencyName}'
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
          metricNamespace: 'Microsoft.Insights/Components'
          metricName: 'dependencies/failed'
          dimensions: [
            {
              name: 'dependency/target'
              operator: dependencyTargetOperatorType
              values: dependencyTargets
            }
          ]
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Insights/Components'
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

resource dependencyFailureCountLow 'microsoft.insights/metricAlerts@2018-03-01' = if (failureAlertType == 'Dynamic') {
	name: '${serviceName} - ${dependencyName} Failure Increase - Low Threshold'
	location: 'global'
	tags: {
		MonitoringCategory: 'Partner Dependency'
		MonitoringSubCategory: 'Per Partner'
	}
	properties: {
		description: 'Dependency Failure Increase ${dependencyName}'
		severity: 3
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
          metricNamespace: 'Microsoft.Insights/Components'
          metricName: 'dependencies/failed'
          dimensions: [
            {
              name: 'dependency/target'
              operator: dependencyTargetOperatorType
              values: dependencyTargets
            }
          ]
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: false
    targetResourceType: 'Microsoft.Insights/Components'
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

resource dependencyHighServiceLatency 'microsoft.insights/metricAlerts@2018-03-01' = {
	name: '${serviceName} - ${dependencyName} Service Latency - High Latency'
	location: 'global'
	tags: {
		MonitoringCategory: 'Partner Dependency'
		MonitoringSubCategory: 'Service Latency'
	}
	properties: {
		description: 'Dependency Increased Latency ${dependencyName}'
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
          alertSensitivity: 'Low'
          failingPeriods: {
            numberOfEvaluationPeriods: 2
            minFailingPeriodsToAlert: 2
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.Insights/Components'
          metricName: 'dependencies/duration'
          dimensions: [
            {
              name: 'dependency/target'
              operator: dependencyTargetOperatorType
              values: dependencyTargets
            }
          ]
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Insights/Components'
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
