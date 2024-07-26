namespace Kgm.Spo.FunctionApps.WebhookExample.Options
{
    public class ConfigurationOptions
    {
        public AzureFunctionOptions AzureFunction { get; set; } = null!;
        public string AzureWebJobsStorage { get; set; } = null!;
    }
}
