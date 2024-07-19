using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;

namespace ficapptest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Define application and resource details
            string appClientId = "60416d10-5297-4d99-8ec8-9d0cbca81fbc"; //replace with your Azure AD App registration Client ID
            string resourceTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47"; //This is MSFT tenant ID - Do not change this
            Uri authorityUri = new($"https://login.microsoftonline.com/{resourceTenantId}");
            string miClientId = "de7665da-cbfa-4b05-b4e6-5867195e6443"; //replace with the UAMI you created 
            string audience = "api://AzureADTokenExchange"; //This is the Token Exchange scope
            string vaultUrl = "https://queryme.vault.azure.net";
            string secretName = "secret";

            Console.WriteLine("Starting token acquisition process...");
            Console.WriteLine("");
            
            // Gets a token for the user-assigned Managed Identity.
            // Delegate to acquire token for the user-assigned Managed Identity
            var miAssertionProvider = async (AssertionRequestOptions _) =>
            {
                Console.WriteLine("Acquiring token for user-assigned Managed Identity...");
                Console.WriteLine("");

                // Create Managed Identity application
                var miApplication = ManagedIdentityApplicationBuilder
                    .Create(ManagedIdentityId.WithUserAssignedClientId(miClientId))
                    .Build();

                //Federated Identity Credentials allow applications to use access tokens issued by trusted identity providers as credentials.
                //The audience value "api://AzureADTokenExchange" indicates that the token is meant for the Azure AD Token Exchange service, which is a part of the Federated Identity Credentials flow
                var miResult = await miApplication.AcquireTokenForManagedIdentity(audience)
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                Console.WriteLine($"Managed Identity token acquired: {miResult.AccessToken}");
                Console.WriteLine("");

                //This token from the Managed Identity, can now be exchanged for another token from AAD that can be used to access the Key Vault.
                //See line 55 where the MI Token is used as an Assertion and is exchanged with ESTS to get a AAD token
                return miResult.AccessToken;
            };

            // Configure the confidential client application with Managed Identity assertion
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(appClientId)
              .WithAuthority(authorityUri, false)
              .WithClientAssertion(miAssertionProvider)
              .WithCacheOptions(CacheOptions.EnableSharedCacheOptions)
              .Build();

            string[] scopes = new[] { "https://queryme.vault.azure.net/.default" };

            Console.WriteLine("Acquiring token for confidential client...");
            Console.WriteLine("");

            // Acquire token for the confidential client
            AuthenticationResult result = await app.AcquireTokenForClient(scopes)
              .ExecuteAsync()
              .ConfigureAwait(false);

            Console.WriteLine($"Confidential client token acquired: {result.AccessToken}");
            Console.WriteLine("");

            // Use the token to create a SecretClient and retrieve a secret
            Console.WriteLine("Creating SecretClient with acquired token...");
            Console.WriteLine("");
            var tokenCredential = new ManagedIdentityCredential(clientId: miClientId);
            var client = new SecretClient(new Uri(vaultUrl), tokenCredential);

            Console.WriteLine($"Retrieving secret '{secretName}' from Key Vault...");
            Console.WriteLine("");
            KeyVaultSecret secret = await client.GetSecretAsync(secretName);
            Console.WriteLine($"Secret value: {secret.Value}");
            Console.WriteLine("");

            Console.WriteLine("Token acquisition process completed.");
            Console.WriteLine("");

            Console.Read();
        }
    }
}
