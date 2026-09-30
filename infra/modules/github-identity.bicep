// The identity GitHub Actions signs in as (OpenID Connect, no stored credentials). Federated credentials trust
// pushes to main, pull requests (previews), and the production environment (CD).
//
// Roles, each scoped to one resource:
// - AcrPush on the registry: push images.
// - AcrDelete on the registry: delete a closed pull request's image tags.
// - Reader on the registry: `az acr login` and `az acr repository` look the registry up through Azure Resource
//   Manager, which the data-plane roles above do not allow.
// - Azure Kubernetes Service Cluster User Role on the cluster: `az aks get-credentials` (it includes the cluster read
//   and user-credential actions). The cluster uses Kubernetes RBAC with local accounts, so that kubeconfig can deploy.

param namePrefix string
param location string
param githubRepository string
param acrName string
param aksName string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${namePrefix}-github'
  location: location
}

var federatedSubjects = [
  {
    name: 'github-main'
    subject: 'repo:${githubRepository}:ref:refs/heads/main'
  }
  {
    name: 'github-pull-requests'
    subject: 'repo:${githubRepository}:pull_request'
  }
  {
    name: 'github-production'
    subject: 'repo:${githubRepository}:environment:production'
  }
]

// Azure rejects concurrent writes of federated credentials on one identity, so create them one at a time.
@batchSize(1)
resource credentials 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = [
  for item in federatedSubjects: {
    parent: identity
    name: item.name
    properties: {
      issuer: 'https://token.actions.githubusercontent.com'
      subject: item.subject
      audiences: [
        'api://AzureADTokenExchange'
      ]
    }
  }
]

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: acrName
}

resource cluster 'Microsoft.ContainerService/managedClusters@2026-05-01' existing = {
  name: aksName
}

var registryRoles = {
  acrPush: '8311e382-0749-4cb8-b61a-304f252e45ec'
  acrDelete: 'c2f4ef07-c644-48eb-af81-4b1b4947fb11'
  reader: 'acdd72a7-3385-48ef-bd42-f606fba81ae7'
}

resource registryAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for role in items(registryRoles): {
    name: guid(registry.id, identity.id, role.value)
    scope: registry
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role.value)
      principalId: identity.properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

var aksClusterUserRoleId = '4abbcc35-e782-43d8-92c5-2d3f1bd2253f'

resource clusterUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cluster.id, identity.id, aksClusterUserRoleId)
  scope: cluster
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', aksClusterUserRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output clientId string = identity.properties.clientId
