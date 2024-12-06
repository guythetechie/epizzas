targetScope = 'resourceGroup'

param location string
param suffix string = take(uniqueString(resourceGroup().id), 6)
param logAnalyticsWorkspaceName string = 'epizzas-${suffix}-law'
param virtualNetworkName string = 'epizzas-${suffix}-vnet'
param privateLinkSubnetName string = 'private-link'
param cosmosAccountName string = 'epizzas-${suffix}-cosmos'
param cosmosDatabaseName string = 'epizzas'
param cosmosOrdersContainerName string = 'orders'
param tags object = {}
param allowedIpCsv string = ''
param principalId string

var allowedIps = map(split(allowedIpCsv, ','), ip => trim(ip))
var privateLinkSubnetResourceId = first(filter(
  virtualNetworkDeployment.outputs.subnetResourceIds,
  id => contains(id, privateLinkSubnetName)
))

module logAnalyticsWorkspaceDeployment 'br/public:avm/res/operational-insights/workspace:0.9.0' = {
  name: 'log-analytics-workspace-deployment'
  params: {
    name: logAnalyticsWorkspaceName
    location: location
    tags: tags
    useResourcePermissions: true
  }
}

module virtualNetworkDeployment 'br/public:avm/res/network/virtual-network:0.5.1' = {
  name: 'virtual-network-deployment'
  params: {
    name: virtualNetworkName
    location: location
    tags: tags
    addressPrefixes: [
      '10.0.0.0/24'
    ]
    diagnosticSettings: [
      {
        name: 'enable-all'
        metricCategories: [
          {
            category: 'AllMetrics'
          }
        ]
        workspaceResourceId: logAnalyticsWorkspaceDeployment.outputs.resourceId
        logAnalyticsDestinationType: 'Dedicated'
      }
    ]
    subnets: [
      {
        name: privateLinkSubnetName
        addressPrefix: '10.0.0.0/28'
      }
    ]
  }
}

module cosmosPrivateDnsZone 'br/public:avm/res/network/private-dns-zone:0.6.0' = {
  name: 'cosmos-private-dns-zone'
  params: {
    name: 'privatelink.documents.azure.com'
    location: 'global'
    tags: tags
    virtualNetworkLinks: [
      {
        virtualNetworkResourceId: virtualNetworkDeployment.outputs.resourceId
      }
    ]
  }
}

module cosmosDeployment 'br/public:avm/res/document-db/database-account:0.8.1' = {
  name: 'cosmos-deployment'
  params: {
    name: cosmosAccountName
    location: location
    tags: tags
    capabilitiesToAdd: [
      'EnableServerless'
    ]
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        failoverPriority: 0
        locationName: location
        isZoneRedundant: false
      }
    ]
    diagnosticSettings: [
      {
        name: 'enable-all'
        metricCategories: [
          {
            category: 'AllMetrics'
          }
        ]
        logCategoriesAndGroups: [
          {
            categoryGroup: 'AllLogs'
          }
        ]
        workspaceResourceId: logAnalyticsWorkspaceDeployment.outputs.resourceId
        logAnalyticsDestinationType: 'Dedicated'
      }
    ]
    networkRestrictions: {
      ipRules: allowedIps
      virtualNetworkRules: []
    }
    sqlDatabases: [
      {
        name: cosmosDatabaseName
        containers: [
          {
            name: cosmosOrdersContainerName
            paths: [
              '/orderId'
            ]
          }
        ]
      }
    ]
    privateEndpoints: [
      {
        name: '${cosmosAccountName}-pep'
        customNetworkInterfaceName: '${cosmosAccountName}-nic'
        tags: tags
        service: 'Sql'
        subnetResourceId: privateLinkSubnetResourceId
        privateDnsZoneGroup: {
          name: cosmosAccountName
          privateDnsZoneGroupConfigs: [
            {
              name: cosmosPrivateDnsZone.outputs.name
              privateDnsZoneResourceId: cosmosPrivateDnsZone.outputs.resourceId
            }
          ]
        }
      }
    ]
    sqlRoleAssignmentsPrincipalIds: [
      principalId
    ]
    sqlRoleDefinitions: [
      {
        name: guid('readerwriter')
        roleName: 'Cosmos DB Reader and Writer'
        dataAction: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/*'
        ]
      }
    ]
  }
}

output cosmosAccountEndpoint string = cosmosDeployment.outputs.endpoint
output cosmosDatabaseName string = cosmosDatabaseName
output cosmosOrdersContainerName string = cosmosOrdersContainerName
