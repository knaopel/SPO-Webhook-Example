using System.Security.Cryptography.X509Certificates;

namespace Kgm.Spo.FunctionApps.WebhookExample.Options
{
  public class AzureFunctionOptions
  {
    public string TenantId { get; set; }
    public string TenantName { get; set; }
    public string ClientId { get; set; }
    public StoreName CertificateStoreName { get; set; }
    public StoreLocation CertificateStoreLocation { get; set; }
    public string CertificateThumbprint { get; set; }
  }
}