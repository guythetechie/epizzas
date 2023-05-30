param location string = resourceGroup().location
param tags object = {}
param logAnalyticsWorkspaceName string
param applicationInsightsName string
param virtualNetworkName string
param containerAppSubnetName string
param privateLinkSubnetName string
param serviceBusNamespaceName string
param cosmosAccountName string

resource ampls 'Microsoft.Insights/privateLinkScopes@2021-07-01-preview' = {
  name: 'ampls'
  tags: tags
  location: 'global'
  properties: {
    accessModeSettings: {
      ingestionAccessMode: 'Open'
      queryAccessMode: 'Open'
    }
  }
}

resource amplsPrivateEndpoint 'Microsoft.Network/privateEndpoints@2022-11-01' = {
  name: '${ampls.name}-pep'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: privateLinkSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: ampls.name
        properties: {
          privateLinkServiceId: ampls.id
          groupIds: [
            'azuremonitor'
          ]
        }
      }
    ]
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  tags: tags
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource logAnalyticsWorkspaceAmpls 'Microsoft.Insights/privateLinkScopes/scopedResources@2021-07-01-preview' = {
  name: '${logAnalyticsWorkspace.name}-ampls'
  parent: ampls
  properties: {
    linkedResourceId: logAnalyticsWorkspace.id
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  tags: tags
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'other'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

resource applicationInsightsAmpls 'Microsoft.Insights/privateLinkScopes/scopedResources@2021-07-01-preview' = {
  name: '${applicationInsights.name}-ampls'
  parent: ampls
  properties: {
    linkedResourceId: applicationInsights.id
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2022-11-01' = {
  name: virtualNetworkName
  tags: tags
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/23'
        '10.0.2.0/24'
      ]
    }
    subnets: [
      {
        name: containerAppSubnetName
        properties: {
          addressPrefix: '10.0.0.0/23'
        }
      }
      {
        name: privateLinkSubnetName
        properties: {
          addressPrefix: '10.0.2.0/26'
        }
      }
    ]
  }
}

resource virtualNetworkDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'enable-all'
  scope: virtualNetwork
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logAnalyticsDestinationType: 'Dedicated'
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]

    logs: [
      {
        category: 'VMProtectionAlerts'
        enabled: true
      }
    ]
  }
}

resource privateLinkSubnet 'Microsoft.Network/virtualNetworks/subnets@2022-11-01' existing = {
  name: privateLinkSubnetName
  parent: virtualNetwork
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  tags: tags
  location: location
  sku: {
    name: 'Standard'
  }
}

resource serviceBusNamespaceDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'enable-all'
  scope: serviceBusNamespace
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logAnalyticsDestinationType: 'Dedicated'
    logs: [
      {
        category: 'OperationalLogs'
        enabled: true
      }
      {
        category: 'VNetAndIPFilteringLogs'
        enabled: true
      }
      {
        category: 'RuntimeAuditLogs'
        enabled: true
      }
      {
        category: 'ApplicationMetricsLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosAccountName
  location: location
  tags: tags
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    backupPolicy: {
      type: 'Continuous'
    }
  }
}

resource cosmosAccountDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'enable-all'
  scope: cosmosAccount
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logAnalyticsDestinationType: 'Dedicated'
    logs: [
      {
        category: 'DataPlaneRequests'
        enabled: true
      }
      {
        category: 'QueryRuntimeStatistics'
        enabled: true
      }
      {
        category: 'PartitionKeyStatistics'
        enabled: true
      }
      {
        category: 'PartitionKeyRUConsumption'
        enabled: true
      }
      {
        category: 'ControlPlaneRequests'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Requests'
        enabled: true
      }
    ]
  }
}

resource cosmosAccountPrivateEndpoint 'Microsoft.Network/privateEndpoints@2022-11-01' = {
  name: '${cosmosAccount.name}-pep'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: privateLinkSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: cosmosAccount.name
        properties: {
          privateLinkServiceId: cosmosAccount.id
          groupIds: [
            'SQL'
          ]
        }
      }
    ]
  }
}
