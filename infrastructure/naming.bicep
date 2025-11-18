@description('Prefix used for all resources in this resource group. Required.')
param prefix string

@description('Short Prefix used for the resources with limited name size in this resource group.')
param shortPrefix string = ''

@description('Azure region to deploy resources to. Used when deploying to a particular region. Required.')
param location string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('All azure regions that resources will be deployed to. Used for shared resources to know all possible names.')
param regions array = []

var regionAbbreviations = {
  australiacentral: 'auc'
  australiacentral2: 'auc2'
  australiaeast: 'aue'
  australiasoutheast: 'ause'
  brazilsouth: 'brs'
  brazilsoutheast: 'brse'
  canadacentral: 'canc'
  canadaeast: 'cane'
  centralindia: 'cin'
  centralus: 'cus'
  centraluseuap: 'cuseuap'
  eastasia: 'ea'
  eastus: 'eus'
  eastus2: 'eus2'
  eastus2euap: 'eus2euap'
  francecentral: 'frc'
  francesouth: 'frs'
  germanynorth: 'gern'
  germanywestcentral: 'gerwc'
  japaneast: 'jae'
  japanwest: 'jaw'
  jioindiacentral: 'jioinc'
  jioindiawest: 'jioinw'
  koreacentral: 'koc'
  koreasouth: 'kors'
  northcentralus: 'ncus'
  northeurope: 'neu'
  norwayeast: 'nore'
  norwaywest: 'norw'
  southafricanorth: 'san'
  southafricawest: 'saw'
  southcentralus: 'scus'
  southeastasia: 'sea'
  southindia: 'sin'
  swedencentral: 'swc'
  switzerlandnorth: 'swn'
  switzerlandwest: 'sww'
  uaecentral: 'uaec'
  uaenorth: 'uaen'
  uksouth: 'uks'
  ukwest: 'ukw'
  westcentralus: 'wcus'
  westeurope: 'weu'
  westindia: 'win'
  westus: 'wus'
  westus2: 'wus2'
  westus3: 'wus3'
}

var partnerRegions = {
  westus2: 'eastus2'
  eastus2: 'westus2'
  westeurope: 'northeurope'
  northeurope: 'westeurope'
  southeastasia: 'japaneast'
  japaneast: 'southeastasia'
}

var primaryRegions = [
  'westus2'
  'westeurope'
  'southeastasia'
]

var shortPrefixVal = empty(shortPrefix) ? prefix : shortPrefix

var partnerRegion = partnerRegions[replace(toLower(location), ' ', '')]

var environmentRGSuffix = environment == 'prod' ? 'PROD' : environment == 'int' ? 'INT' : 'RD'

// Regional (single)
var regionalSuffix = toLower('${environment}-${regionAbbreviations[replace(toLower(location), ' ', '')]}')

output appServiceName string = toLower('${prefix}-appservice-${regionalSuffix}')
output appServicePlanName string = toLower('${prefix}-serviceplan-${regionalSuffix}')
output frontDoorEndpointName string = toLower('${prefix}-frontDoor-${regionalSuffix}')
output redisName string = toLower('${prefix}-redis-${regionalSuffix}')
output apiAvailabilityTestName string = toLower('${prefix}-apiAvailabilityTest-${regionalSuffix}')
output virtualNetworkName string = toLower('${prefix}-vnet-${regionalSuffix}')
output networkSecurityGroupName string = toLower('${prefix}-nsg-${regionalSuffix}')
output vnetKeyVaultAccessScriptName string = toLower('${prefix}-vnetKeyVaultAccessScript-${regionalSuffix}')
output publicIPName string = toLower('${prefix}-publicip-${regionalSuffix}')
output natGatewayName string = toLower('${prefix}-natgateway-${regionalSuffix}')
output cognitiveServiceName string = toLower('${prefix}-cognitiveServices-${regionalSuffix}')

output appServiceMockName string = toLower('${prefix}-mock-appservice-${regionalSuffix}')
output appServicePlanMockName string = toLower('${prefix}-mock-serviceplan-${regionalSuffix}')
output vnetKeyVaultAccessScriptMockName string = toLower('${prefix}-mock-vnetKeyVaultAccessScript-${regionalSuffix}')

// Regional (all)
var regionalSuffixes = map(
  regions,
  region => toLower('${environment}-${regionAbbreviations[replace(toLower(region), ' ', '')]}')
)

output appServiceNames array = map(regionalSuffixes, suffix => toLower('${prefix}-appservice-${suffix}'))
output regionalAbbreviations array = map(regions, region => regionAbbreviations[replace(toLower(region), ' ', '')])

// Shared
output loadTestServiceName string = toLower('${prefix}-loadtest-${environment}')
output serviceManagedIdentityName string = toLower('${prefix}-identity-${environment}')
output keyVaultName string = replace(toLower('${shortPrefixVal}-kv-${environment}'), '-', '')
output appConfigurationName string = (environment == 'int')
  ? (prefix == 'ccapi-common')
      ? toLower('${prefix}-appConfig-corp-${environment}')
      : toLower('${prefix}-appConfig-${environment}-corp')
  : toLower('${prefix}-appConfig-${environment}')
output frontDoorName string = toLower('${prefix}-frontDoor-${environment}')
output frontDoorDefaultRouteName string = 'defaultRoute'
output eventGridServiceName string = toLower('${prefix}-eventgrid-${environment}')
output applicationInsightsName string = toLower('${prefix}-appInsights-${environment}')
output logAnalyticsWorkspace string = toLower('${prefix}-logAnalytics-${environment}')
output communicationServices string = toLower('ccapi-acs-provider-acs-${environment}')
output cognitiveServices string = toLower('${prefix}-cognitiveServices-${environment}')
output communicationServicesSystemTopic string = toLower('${prefix}-acs-event-topic-${environment}')
output storageAccount string = replace(toLower('${shortPrefixVal}-sa-${environment}'), '-', '')
output wafPolicyName string = replace(toLower('${prefix}-wafPolicy-${environment}'), '-', '')
output frontDoorAvailabilityTestName string = toLower('${prefix}-frontDoorAvailabilityTest-${environment}')
output actionGroupName string = toLower('${prefix}')
output actionGroupShortName string = take('${prefix}', 12)

output bueIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-bue')
output apimIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-apim')
output callbackIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-callback')
output ccpIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-ccp')
output ccapiIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-ccapi')
output sawIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-saw')
output azureVpnIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-azurevpn')
output oneConnectorIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-oneconnector')
output orchestratorIpGroupName string = toLower('ccapi-common-ipGroup-${environment}-orchestrator')

output commonResourceGroup string = 'CCAPI-Common-${environment == 'prod' ? 'PROD' : environment == 'int' ? 'INT' : 'RD'}'
output environmentRGSuffix string = environmentRGSuffix
output servicebusInternalName string = toLower('${prefix}-servicebus-${regionalSuffix}')
output servicebusDRAliasName string = contains(primaryRegions, replace(toLower(location), ' ', ''))
  ? 'contctcenterservicebus-${regionAbbreviations[replace(toLower(location), ' ', '')]}-${regionAbbreviations[partnerRegion]}-${environment}'
  : 'contctcenterservicebus-${regionAbbreviations[partnerRegion]}-${regionAbbreviations[replace(toLower(location), ' ', '')]}-${environment}'

// Subnets (no prefix or suffix)
output appServiceSubnetName string = 'app-service'
output appServiceMockSubnetName string = 'app-service-mock'

output commonManagedIdentityName string = toLower('SxGICon_CCAPI_${environmentRGSuffix}_MI_S2S_PME')

output trafficManagerName string = toLower('${prefix}-trafficManager-v1-${environment}')
output currRegTrafficManager string = toLower('${prefix}-regtm-${regionAbbreviations[replace(toLower(location), ' ', '')]}-v1-${environment}')

var trafficManagerRegions = environment == 'int'
  ? location == 'West US 2'
      ? [
          'West US 2'
          'East US 2'
        ]
      : location == 'East US 2'
          ? [
              'East US 2'
              'West US 2'
            ]
          : []
  : regions

output regionalTmsWithPriority array = map(trafficManagerRegions, (region, index) => {
  tmName: toLower('${prefix}-regtm-${regionAbbreviations[replace(toLower(region), ' ', '')]}-v1-${environment}')
  tmLocation: region
  tmPriority: index + 1
})

// OneConnector ID's
output occSubscriptionId string = environment == 'int' ? '' : environment == 'ppe' ? 'bc970a64-0a20-44e7-a673-a1b9f20b0ca0' : 'd9d18e84-40fe-4f31-ab9d-92231d96e8fd'
output occManagedIdentityName string = toLower('icon-oneconnector-identity-${environment}')
output occResourceGroupName string = toLower('icon-rg-oneconnector-${environment}-common')
