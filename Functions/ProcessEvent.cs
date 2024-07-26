using Azure.Storage.Queues;
using Kgm.Spo.FunctionApps.WebhookExample.Models;
using Kgm.Spo.FunctionApps.WebhookExample.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PnP.Core.Services;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Kgm.Spo.FunctionApps.WebhookExample.Functions
{
    public class ProcessEvent
    {
        private readonly ILogger _logger;
        private readonly IPnPContextFactory _pnpContextFactory;
        private readonly AzureFunctionOptions _options;
        private readonly QueueServiceClient _queueServiceClient;

        public ProcessEvent(IPnPContextFactory pnpContextFactory, IOptions<ConfigurationOptions> optionsAccessor, QueueServiceClient queueServiceClient, ILoggerFactory loggerFactory)
        {
            _pnpContextFactory = pnpContextFactory;
            _options = optionsAccessor.Value.AzureFunction;
            _queueServiceClient = queueServiceClient;
            _logger = loggerFactory.CreateLogger<ProcessEvent>();
        }

        [Function("ProcessEvent")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req, string validationToken)
        {
            _logger.LogInformation("Webhook triggered!");

            // Prepare the response object
            HttpResponseData response;

            if (!string.IsNullOrEmpty(validationToken))
            {
                // If we've got a validationToken querystring argument,
                // we simply reply back with 200 (OK) and the echo of the validationToken
                response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await response.WriteStringAsync(validationToken);

                return response;
            }

            // otherwise we need to process the event
            try
            {
                // First of all, try to deserialize the request body
                using (var reader = new StreamReader(req.Body))
                {
                    var jsonRequest = await reader.ReadToEndAsync();
                    _logger.LogInformation(jsonRequest);
                    var notifications = System.Text.Json.JsonSerializer.Deserialize<WebhookNotification>(jsonRequest, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // If we have the input object
                    if (notifications != null)
                    {
                        // The process every single event in the notification body
                        foreach (var notification in notifications.Value)
                        {
                            var queue = _queueServiceClient.GetQueueClient("spo-webhooks");
                            if (await queue.ExistsAsync())
                            {
                                var message = System.Text.Json.JsonSerializer.Serialize(notification);
                                await queue.SendMessageAsync(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message)));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            // we need to return a 200 (OK) response within 5 seconds
            response = req.CreateResponse(HttpStatusCode.OK);
            return response;

        }
    }
}
