param name string
param location string = resourceGroup().location
param tags object = {}
param identityId string
param deploymentName string = 'gpt-4o'
param modelName string = 'gpt-4o'
param modelVersion string = '2024-08-06'
param deploymentCapacity int = 10

resource cognitiveAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'AIServices'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
  }
}

resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: cognitiveAccount
  name: deploymentName
  sku: {
    name: 'Standard'
    capacity: deploymentCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
}

output endpoint string = cognitiveAccount.properties.endpoint
output id string = cognitiveAccount.id
output name string = cognitiveAccount.name
output playgroundUrl string = '${cognitiveAccount.properties.endpoint}openai/deployments/${deploymentName}'
output modelName string = modelName
