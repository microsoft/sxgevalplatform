@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Id of the Application Insights. Required.')
param appInsightsResourceId string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

// Mapping to configure alert severity by environment
var environmentMapping = {
	int: {
    appInsightsAlertDropInAvailability95: {
      severity: 4
	  	enabled: false
    }
    appInsightsAlertDropInAvailability75: {
      severity: 4
	  	enabled: false
    }
		appInsightsLogCountDrop0: {
      severity: 4
	  	enabled: false
    }
  }
  ppe: {
    appInsightsAlertDropInAvailability95: {
      severity: 4
	  	enabled: true
    }
    appInsightsAlertDropInAvailability75: {
      severity: 4
	  	enabled: false
    }
		appInsightsLogCountDrop0: {
      severity: 4
	  	enabled: false
    }
  }
  prod: {
    appInsightsAlertDropInAvailability95: {
      severity: 4
	  	enabled: true
    }
    appInsightsAlertDropInAvailability75: {
      severity: 3
	  	enabled: true
    }
		appInsightsLogCountDrop0: {
      severity: 3
	  	enabled: true
    }
  }
}

resource appInsightsAlertDropInAvailability95 'microsoft.insights/metricAlerts@2018-03-01' = {
	name: '${serviceName} App Insights - Drop in Availability - 95 Percent'
	location: 'global'
	tags: {
		MonitoringCategory: 'Reliability & Availability'
		MonitoringSubCategory: 'Availability'
	}
	properties: {
		description: 'App Insights Drop in Availability'
		severity: environmentMapping[environment].appInsightsAlertDropInAvailability95.severity
		enabled: environmentMapping[environment].appInsightsAlertDropInAvailability95.enabled
		scopes: [
			appInsightsResourceId
		]
		evaluationFrequency: 'PT15M'
		windowSize: 'PT15M'
		criteria: {
			allOf: [
				{
					threshold: 95
					name: 'Metric1'
					metricNamespace: 'Microsoft.Insights/components'
					metricName: 'availabilityResults/availabilityPercentage'
					operator: 'LessThan'
					timeAggregation: 'Average'
					skipMetricValidation: false
					criterionType: 'StaticThresholdCriterion'
				}
			]
			'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
		}
		autoMitigate: true
		targetResourceType: 'microsoft.insights/components'
		targetResourceRegion: location
		actions: [
			{
				actionGroupId: actionGroupId
				webHookProperties: {}
			}
		]
	}
}

resource appInsightsAlertDropInAvailability75 'microsoft.insights/metricAlerts@2018-03-01' = {
	name: '${serviceName} App Insights - Drop in Availability - 75 Percent'
	location: 'global'
	tags: {
		MonitoringCategory: 'Reliability & Availability'
		MonitoringSubCategory: 'Availability'
	}
	properties: {
		description: 'App Insights Drop in Availability'
		severity: environmentMapping[environment].appInsightsAlertDropInAvailability75.severity
		enabled: environmentMapping[environment].appInsightsAlertDropInAvailability75.enabled
		scopes: [
			appInsightsResourceId
		]
		evaluationFrequency: 'PT15M'
		windowSize: 'PT15M'
		criteria: {
			allOf: [
				{
					threshold: 75
					name: 'Metric1'
					metricNamespace: 'Microsoft.Insights/components'
					metricName: 'availabilityResults/availabilityPercentage'
					operator: 'LessThan'
					timeAggregation: 'Average'
					skipMetricValidation: false
					criterionType: 'StaticThresholdCriterion'
				}
			]
			'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
		}
		autoMitigate: true
		targetResourceType: 'microsoft.insights/components'
		targetResourceRegion: location
		actions: [
			{
				actionGroupId: actionGroupId
				webHookProperties: {}
			}
		]
	}
}

resource appInsightsLogCountDrop0 'microsoft.insights/scheduledqueryrules@2023-03-15-preview' = {
	name: '${serviceName} App Insights - Drop in LogCount - Equal To 0'
	location: location
	tags: {
		MonitoringCategory: 'Reliability & Availability'
		MonitoringSubCategory: 'Availability'
	}
	properties: {
		description: 'App Insights Drop in LogCount'
		severity: environmentMapping[environment].appInsightsLogCountDrop0.severity
		enabled: environmentMapping[environment].appInsightsLogCountDrop0.enabled
		scopes: [
			appInsightsResourceId
		]
		evaluationFrequency: 'PT15M'
		windowSize: 'PT15M'
		criteria: {
			allOf: [
				{
					query: 'traces\n| summarize LogCount = count()\n'
					timeAggregation: 'Average'
					metricMeasureColumn: 'LogCount'
					dimensions: []
					operator: 'Equal'
					threshold: 0
					failingPeriods: {
						numberOfEvaluationPeriods: 1
						minFailingPeriodsToAlert: 1
					}
				}
			]
		}
		autoMitigate: true
		targetResourceTypes: [
			'Microsoft.Insights/components'
		]
		actions: {
			actionGroups: [
				actionGroupId
			]
			customProperties: {}
			actionProperties: {}
		}
	}
}
