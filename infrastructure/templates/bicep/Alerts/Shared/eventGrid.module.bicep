@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Event Grid Resource Id. Required.')
param eventGridSystemTopicResourceId string

@description('Environment into which to deploy resources. Required.')
param environment string

// Mapping to configure alert severity by environment
var environmentMapping = {
  int: {
    eventGridAlertIncreaseInPublishFailedEventsMedium: {
      severity: 4
	    enabled: false
    }
    eventGridAlertIncreaseInPublishFailedEventsLow: {
      severity: 4
	    enabled: false
    }
	  eventGridAlertIncreaseInDeliveryFailedEventsMedium: {
      severity: 4
	    enabled: false
    }
    eventGridAlertIncreaseInDeliveryFailedEventsLow: {
      severity: 4
	    enabled: false
    }
  }
  ppe: {
    eventGridAlertIncreaseInPublishFailedEventsMedium: {
      severity: 4
	    enabled: false
    }
    eventGridAlertIncreaseInPublishFailedEventsLow: {
      severity: 4
	    enabled: true
    }
	  eventGridAlertIncreaseInDeliveryFailedEventsMedium: {
      severity: 4
	    enabled: false
    }
    eventGridAlertIncreaseInDeliveryFailedEventsLow: {
      severity: 4
	    enabled: true
    }
  }
  prod: {
    eventGridAlertIncreaseInPublishFailedEventsMedium: {
      severity: 4
	    enabled: true
    }
    eventGridAlertIncreaseInPublishFailedEventsLow: {
      severity: 3
	    enabled: true
    }
	  eventGridAlertIncreaseInDeliveryFailedEventsMedium: {
      severity: 4
	    enabled: true
    }
    eventGridAlertIncreaseInDeliveryFailedEventsLow: {
      severity: 3
	    enabled: true
    }
  }
}

resource eventGridAlertIncreaseInPublishFailedEventsMedium 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: 'Event Grid Increase in Publish Failed Events - Medium Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Event Grid Increase in Publish Failed Events'
    severity: environmentMapping[environment].eventGridAlertIncreaseInPublishFailedEventsMedium.severity
    enabled: environmentMapping[environment].eventGridAlertIncreaseInPublishFailedEventsMedium.enabled
    scopes: [
      eventGridSystemTopicResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Medium'
          failingPeriods: {
            numberOfEvaluationPeriods: 4
            minFailingPeriodsToAlert: 4
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.EventGrid/systemTopics'
          metricName: 'PublishFailCount'
          operator: 'GreaterThan'
          timeAggregation: 'Total'
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: environmentMapping[environment].eventGridAlertIncreaseInPublishFailedEventsMedium.severity == 4
    targetResourceType: 'Microsoft.EventGrid/systemTopics'
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

resource eventGridAlertIncreaseInPublishFailedEventsLow 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: 'Event Grid Increase in Publish Failed Events - Low Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Event Grid Increase in Publish Failed Events'
    severity: environmentMapping[environment].eventGridAlertIncreaseInPublishFailedEventsLow.severity
    enabled: environmentMapping[environment].eventGridAlertIncreaseInPublishFailedEventsLow.enabled
    scopes: [
      eventGridSystemTopicResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Low'
          failingPeriods: {
            numberOfEvaluationPeriods: 4
            minFailingPeriodsToAlert: 4
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.EventGrid/systemTopics'
          metricName: 'PublishFailCount'
          operator: 'GreaterThan'
          timeAggregation: 'Total'
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: environmentMapping[environment].eventGridAlertIncreaseInPublishFailedEventsLow.severity == 4
    targetResourceType: 'Microsoft.EventGrid/systemTopics'
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

resource eventGridAlertIncreaseInDeliveryFailedEventsMedium 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: 'Event Grid Increase in Delivery Failed Events - Medium Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Event Grid Increase in Delivery Failed Events'
    severity: environmentMapping[environment].eventGridAlertIncreaseInDeliveryFailedEventsMedium.severity
    enabled: environmentMapping[environment].eventGridAlertIncreaseInDeliveryFailedEventsMedium.enabled
    scopes: [
      eventGridSystemTopicResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Medium'
          failingPeriods: {
            numberOfEvaluationPeriods: 4
            minFailingPeriodsToAlert: 4
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.EventGrid/systemTopics'
          metricName: 'DeliveryAttemptFailCount'
          operator: 'GreaterThan'
          timeAggregation: 'Total'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: environmentMapping[environment].eventGridAlertIncreaseInDeliveryFailedEventsMedium.severity == 4
    targetResourceType: 'Microsoft.EventGrid/systemTopics'
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

resource eventGridAlertIncreaseInDeliveryFailedEventsLow 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: 'Event Grid Increase in Delivery Failed Events - Low Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Event Grid Increase in Delivery Failed Events'
    severity: environmentMapping[environment].eventGridAlertIncreaseInDeliveryFailedEventsLow.severity
    enabled: environmentMapping[environment].eventGridAlertIncreaseInDeliveryFailedEventsLow.enabled
    scopes: [
      eventGridSystemTopicResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Low'
          failingPeriods: {
            numberOfEvaluationPeriods: 4
            minFailingPeriodsToAlert: 4
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.EventGrid/systemTopics'
          metricName: 'DeliveryAttemptFailCount'
          operator: 'GreaterThan'
          timeAggregation: 'Total'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: environmentMapping[environment].eventGridAlertIncreaseInDeliveryFailedEventsLow.severity == 4
    targetResourceType: 'Microsoft.EventGrid/systemTopics'
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
