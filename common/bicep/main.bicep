param location string = resourceGroup().location
param tags object = {}
param logAnalyticsWorkspaceName string
param applicationInsightsName string
param virtualNetworkName string
param containerAppSubnetName string
param privateLinkSubnetName string
param eventGridNamespaceName string
param eventGridNamespaceLocation string = 'eastus'

resource ampls 'Microsoft.Insights/privateLinkScopes@2021-07-01-preview' = {
  name: 'ampls'
  tags: tags
  location: location
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
        '10.0.1.0/23'
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
          addressPrefix: '10.0.1.0/28'
        }
      }
    ]
  }
}

resource virtualNetworkDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'enable-all'
  scope: virtualNetwork
  properties: {
    workspaceId: virtualNetwork.id
    logAnalyticsDestinationType: 'Dedicated'
    metrics: [
      {
        category: 'Transaction'
        enabled: true
      }
    ]

    logs: [
      {
        category: 'allLogs'
        enabled: true
      }
    ]
  }
}

resource privateLinkSubnet 'Microsoft.Network/virtualNetworks/subnets@2022-11-01' existing = {
  name: privateLinkSubnetName
  parent: virtualNetwork
}

resource eventGridNamespace 'Microsoft.EventGrid/namespaces@2023-06-01-preview' = {
  name: eventGridNamespaceName
  tags: tags
  location: eventGridNamespaceLocation
  sku: {
    capacity: 1
    name: 'Standard'
  }
  properties: {
    isZoneRedundant: true
    publicNetworkAccess: 'Enabled'
  }
}
