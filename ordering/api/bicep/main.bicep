param location string = resourceGroup().location
param tags object = {}
param containerRegistryName string
param containerRegistryReaderManagedIdentityName string
param containerAppEnvironmentName string
param apiContainerAppName string
param apiContainerImageName string
param apiContainerImageTag string

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = {
  name: containerRegistryName
}

resource containerRegistryReaderManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: containerRegistryReaderManagedIdentityName
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2022-11-01-preview' existing = {
  name: containerAppEnvironmentName
}

resource apiContainerApp 'Microsoft.App/containerApps@2022-11-01-preview' = {
  name: apiContainerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${containerRegistryReaderManagedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: containerRegistryReaderManagedIdentity.id
        }
      ]
      ingress: {
        external: true
        targetPort: 8080
      }
    }
    template: {
      containers: [
        {
          image: '${containerRegistry.properties.loginServer}/${apiContainerImageName}:${apiContainerImageTag}'
          name: apiContainerImageName
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 10
        rules: [
          {
            name: 'http-scale-rule'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}
