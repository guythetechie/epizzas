param location string = resourceGroup().location
param tags object = {}
param logAnalyticsWorkspaceName string
param applicationInsightsName string
param virtualNetworkName string
param containerAppSubnetName string
param privateLinkSubnetName string
param serviceBusNamespaceName string
param cosmosAccountName string
param containerRegistryName string
param containerAppEnvironmentName string

resource amplsMonitorPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.monitor.azure.com'
  tags: tags
  location: 'global'
}

resource amplsMonitorPrivateDnsZoneVirtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${amplsMonitorPrivateDnsZone.name}-${virtualNetwork.name}-link'
  tags: tags
  location: 'global'
  parent: amplsMonitorPrivateDnsZone
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource amplsOmsPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.oms.opinsights.azure.com'
  tags: tags
  location: 'global'
}

resource amplsOmsPrivateDnsZoneVirtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${amplsOmsPrivateDnsZone.name}-${virtualNetwork.name}-link'
  tags: tags
  location: 'global'
  parent: amplsOmsPrivateDnsZone
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource amplsOdsPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.ods.opinsights.azure.com'
  tags: tags
  location: 'global'
}

resource amplsOdsPrivateDnsZoneVirtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${amplsOdsPrivateDnsZone.name}-${virtualNetwork.name}-link'
  tags: tags
  location: 'global'
  parent: amplsOdsPrivateDnsZone
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource amplsAgentServicePrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.agentsvc.azure-automation.net'
  tags: tags
  location: 'global'
}

resource amplsAgentServicePrivateDnsZoneVirtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${amplsAgentServicePrivateDnsZone.name}-${virtualNetwork.name}-link'
  tags: tags
  location: 'global'
  parent: amplsAgentServicePrivateDnsZone
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource blobPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.blob.${environment().suffixes.storage}'
  tags: tags
  location: 'global'
}

resource blobPrivateDnsZoneVirtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${blobPrivateDnsZone.name}-${virtualNetwork.name}-link'
  tags: tags
  location: 'global'
  parent: blobPrivateDnsZone
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

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

resource amplsPrivateEndpointPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-11-01' = {
  name: '${amplsPrivateEndpoint.name}-private-dns-zone-group'
  parent: amplsPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: amplsMonitorPrivateDnsZone.name
        properties: {
          privateDnsZoneId: amplsMonitorPrivateDnsZone.id
        }
      }
      {
        name: amplsOmsPrivateDnsZone.name
        properties: {
          privateDnsZoneId: amplsOmsPrivateDnsZone.id
        }
      }
      {
        name: amplsOdsPrivateDnsZone.name
        properties: {
          privateDnsZoneId: amplsOdsPrivateDnsZone.id
        }
      }
      {
        name: amplsAgentServicePrivateDnsZone.name
        properties: {
          privateDnsZoneId: amplsAgentServicePrivateDnsZone.id
        }
      }
      {
        name: blobPrivateDnsZone.name
        properties: {
          privateDnsZoneId: blobPrivateDnsZone.id
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

resource containerAppSubnet 'Microsoft.Network/virtualNetworks/subnets@2022-11-01' existing = {
  name: containerAppSubnetName
  parent: virtualNetwork
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

resource cosmosAccountPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.documents.azure.com'
  tags: tags
  location: 'global'
}

resource cosmosAccountPrivateDnsZoneVirtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${cosmosAccountPrivateDnsZone.name}-${virtualNetwork.name}-link'
  tags: tags
  location: 'global'
  parent: cosmosAccountPrivateDnsZone
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource cosmosAccountPrivateEndpointPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-11-01' = {
  name: '${cosmosAccountPrivateEndpoint.name}-private-dns-zone-group'
  parent: cosmosAccountPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: cosmosAccountPrivateDnsZone.name
        properties: {
          privateDnsZoneId: cosmosAccountPrivateDnsZone.id
        }
      }
    ]
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: containerRegistryName
  tags: tags
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2022-11-01-preview' = {
  name: containerAppEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: {
      infrastructureSubnetId: containerAppSubnet.id
    }
  }
}
