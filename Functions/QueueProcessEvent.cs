using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Kgm.Spo.FunctionApps.WebhookExample.Models;
using Kgm.Spo.FunctionApps.WebhookExample.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PnP.Core.Model.SharePoint;
using PnP.Core.Services;

namespace Kgm.Spo.FunctionApps.WebhookExample.Functions
{
  public class QueueProcessEvent
  {
    private readonly ILogger _logger;
    private readonly IPnPContextFactory _pnpContextFactory;
    private readonly AzureFunctionOptions _options;
    private readonly BlobServiceClient _blobServiceClient;
    public QueueProcessEvent(IPnPContextFactory pnPContextFactory, IOptions<ConfigurationOptions> optionsAccessor, BlobServiceClient blobServiceClient, ILoggerFactory loggerFactory)
    {
      _pnpContextFactory = pnPContextFactory;
      _options = optionsAccessor.Value.AzureFunction;
      _blobServiceClient = blobServiceClient;
      _logger = loggerFactory.CreateLogger<QueueProcessEvent>();
    }

    [Function("QueueProcessEvent")]
    public async Task Run([QueueTrigger("spo-webhooks", Connection = "AzureWebJobsStorage")] string queueMessage)
    {
      if (!string.IsNullOrEmpty(queueMessage))
      {
        var notification = System.Text.Json.JsonSerializer.Deserialize<WebhookNotificationEvent>(
          queueMessage,
          new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
          );

        if (notification != null)
        {
          _logger.LogInformation("Notification for resource {resource} on site {siteUrl} for tenant {tenantId} received", notification.Resource, notification.SiteUrl, notification.TenantId);

          using (var pnpContext = await _pnpContextFactory.CreateAsync(new Uri($"https://{_options.TenantName}/{notification.SiteUrl}"), CancellationToken.None))
          {
            pnpContext.GraphFirst = false;

            // define a query for the last 100 changes happened, regardless of the type (add, update, delete). Here code does not provide the ChangeToken
            var changeQuery = new ChangeQueryOptions(false, true)
            {
              Item = true,
              FetchLimit = 100
            };

            var lastChangeToken = await GetLatestChangeTokenAsync();
            if (lastChangeToken != null)
            {
              changeQuery.ChangeTokenStart = new ChangeTokenOptions(lastChangeToken);
            }

            // Use GetChanges against the list with ID notification.Resource, which is the target list for the webhook
            var targetList = pnpContext.Web.Lists.GetById(Guid.Parse(notification.Resource));
            var changes = await targetList.GetChangesAsync(changeQuery);

            // save the latest change token
            await SaveLatestChangeTokenAsync(changes.Last().ChangeToken);

            // Process all the retrieved changes
            foreach (var change in changes)
            {
              _logger.LogInformation("{changeName}", change.GetType().FullName);

              // Try to see if the current change is and IChangeItem
              // meaning the it is a change that occurred on an item
              if (change is IChangeItem changeItem)
              {
                // Get the date and time when the change happened
                DateTime changeTime = changeItem.Time;

                // Check if we have the ID of the target item
                if (changeItem.IsPropertyAvailable<IChangeItem>(i => i.ItemId))
                {
                  var itemId = changeItem.ItemId;

                  // If that is the case, retrieve the item
                  var targetItem = await targetList.Items.GetById(itemId).GetAsync();

                  if (targetItem != null)
                  {
                    // Add some logging information - in real app we would act on it.
                    _logger.LogInformation("Processing changes for item {title} happened on {changeTime}", targetItem.Title, changeTime);
                  }
                }
              }
            }
          }
        }
      }
    }

    private async Task<string?> GetLatestChangeTokenAsync()
    {
      // Get a reference to the Azure Storage container
      var containerClient = _blobServiceClient.GetBlobContainerClient("spo-webhooks-storage");

      // Browse the files (there should be just one, if any)
      await foreach (var blobItem in containerClient.GetBlobsAsync())
      {
        // If the file is the one for which we are looking...
        if (blobItem.Name == "ChangeToken.txt")
        {
          // Get the actual Content
          var blobClient = containerClient.GetBlobClient(blobItem.Name);

          // Download the blob content
          var blobContent = await blobClient.DownloadContentAsync();
          var blobContentString = blobContent.Value.Content.ToString();
          return blobContentString;
        }
      }

      // as a fallback, return null
      return null;
    }

    private async Task SaveLatestChangeTokenAsync(IChangeToken changeToken)
    {
      // Get a reference to the Azure Storage container
      var containerClient = _blobServiceClient.GetBlobContainerClient("spo-webhooks-storage");

      // Get a reference to the blob
      var blobClient = containerClient.GetBlobClient("ChangeToken.txt");

      // Prepare the JSON content
      using (var memoryStream = new MemoryStream())
      {
        using (var streamWriter = new StreamWriter(memoryStream))
        {
          streamWriter.WriteLine(changeToken.StringValue);
          await streamWriter.FlushAsync();

          memoryStream.Position = 0;

          // upload it into the target blob
          await blobClient.UploadAsync(memoryStream, overwrite: true);
        }
      }
    }
  }
}
