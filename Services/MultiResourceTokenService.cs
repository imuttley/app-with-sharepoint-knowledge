using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Core;

namespace AgentWithSPKnowledgeViaRetrieval.Services;

public interface IMultiResourceTokenService
{
    Task<string> GetGraphTokenAsync();
    Task<string> GetAzureAITokenAsync();
    Task EnsureTokensAcquiredAsync();
}

public class MultiResourceTokenService : IMultiResourceTokenService
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MultiResourceTokenService> _logger;
    
    private static readonly string[] GraphScopes = {
        "https://graph.microsoft.com/Files.Read.All",
        "https://graph.microsoft.com/Sites.Read.All",
        "https://graph.microsoft.com/Mail.Send",
        "https://graph.microsoft.com/User.Read"
    };
    
    private static readonly string[] AzureAIScopes = {
        "https://cognitiveservices.azure.com/.default"
    };

    public MultiResourceTokenService(
        ITokenAcquisition tokenAcquisition,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<MultiResourceTokenService> logger)
    {
        _tokenAcquisition = tokenAcquisition;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> GetGraphTokenAsync()
    {
        try
        {
            var token = await _tokenAcquisition.GetAccessTokenForUserAsync(GraphScopes);
            _logger.LogDebug("Successfully acquired Microsoft Graph token");
            return token;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogWarning("Microsoft Graph token requires additional consent: {Error}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire Microsoft Graph token");
            throw;
        }
    }

    public async Task<string> GetAzureAITokenAsync()
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                // Use DefaultAzureCredential for local development (excludes environment credentials)
                _logger.LogDebug("Acquiring Azure AI token using DefaultAzureCredential (local development)");
                
                var credentialOptions = new DefaultAzureCredentialOptions
                {
                    ExcludeEnvironmentCredential = true
                };
                var credential = new DefaultAzureCredential(credentialOptions);
                var tokenRequestContext = new TokenRequestContext(AzureAIScopes);
                var tokenResult = await credential.GetTokenAsync(tokenRequestContext);
                
                _logger.LogDebug("Successfully acquired Azure AI token using DefaultAzureCredential");
                return tokenResult.Token;
            }
            else
            {
                // Use managed identity for Azure AI services in production
                var managedIdentityClientId = _configuration["AzureAd:ClientCredentials:0:ManagedIdentityClientId"];
                
                if (string.IsNullOrEmpty(managedIdentityClientId))
                {
                    var errorMessage = "Managed Identity Client ID is not configured. Cannot acquire Azure AI token in production environment.";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
                
                _logger.LogDebug("Acquiring Azure AI token using managed identity: {ClientId}", managedIdentityClientId);
                
                var credential = new ManagedIdentityCredential(managedIdentityClientId);
                var tokenRequestContext = new TokenRequestContext(AzureAIScopes);
                var tokenResult = await credential.GetTokenAsync(tokenRequestContext);
                
                _logger.LogDebug("Successfully acquired Azure AI token using managed identity");
                return tokenResult.Token;
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to acquire Azure AI token");
            throw;
        }
    }

    public async Task EnsureTokensAcquiredAsync()
    {
        _logger.LogInformation("Attempting to pre-acquire tokens for all resources");
        
        try
        {
            // Try to acquire Graph token (uses user delegation - requires consent)
            await GetGraphTokenAsync();
            _logger.LogInformation("Microsoft Graph token pre-acquired successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not pre-acquire Microsoft Graph token");
        }

        // Azure AI token uses managed identity, no pre-acquisition needed
        _logger.LogInformation("Azure AI token will be acquired on-demand using managed identity");
    }
}