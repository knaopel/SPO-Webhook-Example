namespace Kgm.Spo.FunctionApps.WebhookExample.Models
{
    public class WebhookNotification
    {
        public WebhookNotificationEvent[] Value { get; set; } = [];
    }

    public class WebhookNotificationEvent
    {
        public string SubscriptionId { get; set; } = null!;
        public string ClientState { get; set; } = null!;
        public string ExpirationDateTime { get; set; } = null!;
        public string Resource { get; set; } = null!;
        public string TenantId { get; set; } = null!;
        public string SiteUrl { get; set; } = null!;
        public string WebId { get; set; } = null!;
    }
}