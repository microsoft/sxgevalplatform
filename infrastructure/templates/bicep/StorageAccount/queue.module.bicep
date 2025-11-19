@description('Unique name for this resource. Required.')
param name string

@description('Name of the Storage Account Required.')
param storageAccountName string

resource storageAccountQueueServices 'Microsoft.Storage/storageAccounts/queueServices@2022-09-01' existing = {
  name: '${storageAccountName}/default'
}

resource queue 'Microsoft.Storage/storageAccounts/queueServices/queues@2022-09-01' = {
  name: name
  parent: storageAccountQueueServices
}

output resourceId string = queue.id
