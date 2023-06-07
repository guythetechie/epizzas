param tags object = {}
param virtualNetworkName string
param containerAppEnvironmentName string
param containerAppEnvironmentDefaultDomain string

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2022-11-01-preview' existing = {
  name: containerAppEnvironmentName
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2022-11-01' existing = {
  name: virtualNetworkName
}

resource containerAppEnvironmentPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: containerAppEnvironmentDefaultDomain
  tags: tags
  location: 'global'
}

resource containerAppEnvironmentPrivateDnsZoneVirtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${containerAppEnvironmentPrivateDnsZone.name}-${virtualNetwork.name}-link'
  tags: tags
  location: 'global'
  parent: containerAppEnvironmentPrivateDnsZone
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource containerAppEnvironmentPrivateDnsZoneStarARecord 'Microsoft.Network/privateDnsZones/A@2020-06-01' = {
  name: '*'
  parent: containerAppEnvironmentPrivateDnsZone
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: containerAppEnvironment.properties.staticIp
      }
    ]
  }
}
