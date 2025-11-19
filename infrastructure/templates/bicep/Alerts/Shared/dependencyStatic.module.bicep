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

param failureResponseCodes string

param failureResponseThreshholdSev4 int

param failureResponseThreshholdSev3 int

param serviceName string

var responseCodesArray = !empty(failureResponseCodes) ? split(failureResponseCodes, ',') : []
resource dependencyFailureCodeCountSev4Static 'microsoft.insights/metricAlerts@2018-03-01' = [for code in responseCodesArray: {
	name: '${serviceName} - ${dependencyName} ${code} Increase - Sev4'
	location: 'global'
	tags: {
		MonitoringCategory: 'Partner Dependency'
		MonitoringSubCategory: 'Per Partner'
	}
	properties: {
		description: 'Dependency Failure Code ${code} Increase ${dependencyName}'
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
          threshold: failureResponseThreshholdSev4
          dimensions: [
            {
              name: 'dependency/target'
              operator: dependencyTargetOperatorType
              values: dependencyTargets
            }
            {
              name: 'dependency/resultCode'
              operator: 'Include'
              values: [code]
            }
          ]
          name: 'Metric1'
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/failed'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
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
}]

resource dependencyFailureCodeCountSev3Static 'microsoft.insights/metricAlerts@2018-03-01' = [for code in responseCodesArray: {
	name: '${serviceName} - ${dependencyName} ${code} Increase - Sev3'
	location: 'global'
	tags: {
		MonitoringCategory: 'Partner Dependency'
		MonitoringSubCategory: 'Per Partner'
	}
	properties: {
		description: 'Dependency Failure Code ${code} Increase ${dependencyName}'
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
          threshold: failureResponseThreshholdSev3
          dimensions: [
            {
              name: 'dependency/target'
              operator: dependencyTargetOperatorType
              values: dependencyTargets
            }
            {
              name: 'dependency/resultCode'
              operator: 'Include'
              values: [code]
            }
          ]
          name: 'Metric1'
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/failed'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
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
}]

resource dependencyFailureCountSev3Static 'microsoft.insights/metricAlerts@2018-03-01' = if (empty(responseCodesArray)) {
	name: '${serviceName} - ${dependencyName} Failure Increase - Sev3'
	location: 'global'
	tags: {
		MonitoringCategory: 'Partner Dependency'
		MonitoringSubCategory: 'Per Partner'
	}
	properties: {
		description: 'Dependency Failure Increase ${dependencyName}'
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
          threshold: failureResponseThreshholdSev3
          dimensions: [
            {
              name: 'dependency/target'
              operator: dependencyTargetOperatorType
              values: dependencyTargets
            }
          ]
          name: 'Metric1'
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/failed'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
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

resource dependencyFailureCountSev4Static 'microsoft.insights/metricAlerts@2018-03-01' = if (empty(responseCodesArray)) {
	name: '${serviceName} - ${dependencyName} Failure Increase - Sev4'
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
          threshold: failureResponseThreshholdSev4
          dimensions: [
            {
              name: 'dependency/target'
              operator: dependencyTargetOperatorType
              values: dependencyTargets
            }
          ]
          name: 'Metric1'
          metricNamespace: 'Microsoft.Insights/components'
          metricName: 'dependencies/failed'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
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
