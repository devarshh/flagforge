// A Free-tier AKS cluster with one autoscaling system pool. Azure CNI Overlay keeps pod IPs off the VNet. The OIDC
// issuer and workload identity are on for future use; nothing depends on them yet.

param namePrefix string
param location string
param nodeVmSize string
param minNodes int
param maxNodes int
param acrName string

resource cluster 'Microsoft.ContainerService/managedClusters@2026-05-01' = {
  name: '${namePrefix}-aks'
  location: location
  sku: {
    name: 'Base'
    tier: 'Free'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: '${namePrefix}-aks'
    enableRBAC: true
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      loadBalancerSku: 'standard'
    }
    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        osType: 'Linux'
        type: 'VirtualMachineScaleSets'
        vmSize: nodeVmSize
        count: minNodes
        enableAutoScaling: true
        minCount: minNodes
        maxCount: maxNodes
      }
    ]
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: acrName
}

// AcrPull: the kubelet identity pulls images from the registry without image pull secrets.
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, cluster.id, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: cluster.properties.identityProfile!.kubeletidentity.objectId!
    principalType: 'ServicePrincipal'
  }
}

output name string = cluster.name
