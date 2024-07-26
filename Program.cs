using Kgm.Spo.FunctionApps.WebhookExample.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PnP.Core.Auth.Services.Builder.Configuration;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Kgm.Spo.FunctionApps.WebhookExample
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureAppConfiguration(ConfigureAppConfiguration)
                .ConfigureServices((context, services) =>
                {
                    // set up configOptions object for this class
                    var configurationOptions = new ConfigurationOptions();
                    context.Configuration.Bind(configurationOptions);

                    // Add Azure Storage Services
                    services.AddAzureClients(builder =>
                    {
                        var blobConnectionString = configurationOptions.AzureWebJobsStorage;
                        builder.AddBlobServiceClient(blobConnectionString);
                        builder.AddQueueServiceClient(blobConnectionString);
                    });

                    // Add the global configuration instance
                    services.AddOptions<ConfigurationOptions>().Bind(context.Configuration);

                    // Add PnP Core SDK with default configuration
                    services.AddPnPCore();

                    services.AddPnPCoreAuthentication(options =>
                    {
                        var azureFunction = configurationOptions.AzureFunction;

                        // Load the Certificate
                        X509Certificate2 cert = LoadCertificate(azureFunction);

                        // Configure certificate based auth
                        options.Credentials.Configurations.Add("CertAuth", new PnPCoreAuthenticationCredentialConfigurationOptions
                        {
                            ClientId = azureFunction.ClientId,
                            TenantId = azureFunction.TenantId,
                            X509Certificate = new PnPCoreAuthenticationX509CertificateOptions
                            {
                                Certificate = cert
                            }
                        });

                        options.Credentials.DefaultConfiguration = "CertAuth";
                    });

                    // Add Application Insights
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();
                })
                .Build();

            host.Run();
        }

        private static void ConfigureAppConfiguration(IConfigurationBuilder builder)
        {
            builder
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile("appsettings.local.json", true, true)
                .AddEnvironmentVariables()
                .Build();
        }

        private static X509Certificate2 LoadCertificate(AzureFunctionOptions azureFunctionOptions)
        {
            // Will only be populated when running in the Azure Function host
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            string certBase64Encoded = Environment.GetEnvironmentVariable("CertificateFromKeyVault");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            if (!string.IsNullOrEmpty(certBase64Encoded))
            {
                // Azure Function flow
                return new X509Certificate2(Convert.FromBase64String(certBase64Encoded), "", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
            }
            else
            {
                // Local development flow
                var store = new X509Store(azureFunctionOptions.CertificateStoreName, azureFunctionOptions.CertificateStoreLocation);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, azureFunctionOptions.CertificateThumbprint, false);
                store.Close();

                return certCollection.First();
            }
        }
    }
}
