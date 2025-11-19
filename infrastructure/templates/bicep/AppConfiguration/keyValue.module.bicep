@description('The name of the app config to store this value within. Required.')
param appConfigName string

@description('The key of the configuration entry. Required.')
param key string

@description('The value of the configuration entry. Required.')
param value string

@description('The mime type of the configuration value.')
param contentType string = ''

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: appConfigName
}

resource values 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: key
  parent: appConfig
  properties: {
    value: value
    contentType: contentType
  }
}
