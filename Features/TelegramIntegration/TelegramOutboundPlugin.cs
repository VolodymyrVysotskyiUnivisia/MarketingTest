using MarketingTest.Shared;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MarketingTest.Features.TelegramIntegration
{
    public class TelegramOutboundPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var tracer = (ITracingService)serviceProvider
                .GetService(typeof(ITracingService));

            var context = (IPluginExecutionContext)serviceProvider
                .GetService(typeof(IPluginExecutionContext));

            var serviceFactory = (IOrganizationServiceFactory)serviceProvider
                .GetService(typeof(IOrganizationServiceFactory));

            var service = serviceFactory
                .CreateOrganizationService(context.UserId);

            try
            {
                tracer.Trace("TelegramOutboundPlugin started.");

                var payload =
                    context.InputParameters[Constants.Payload]?.ToString();

                tracer.Trace($"Payload received: {payload}");

                if (string.IsNullOrWhiteSpace(payload))
                {
                    SetErrorResponse(
                        context,
                        "Payload is empty.");

                    return;
                }

                var json = JObject.Parse(payload);

                var messageText =
                    json["Message"]?["text"]?.ToString();

                var chatId =
                    json["To"]?.ToString();

                if (string.IsNullOrWhiteSpace(messageText))
                {
                    SetErrorResponse(
                        context,
                        "Message text is empty.");

                    return;
                }

                if (string.IsNullOrWhiteSpace(chatId))
                {
                    SetErrorResponse(
                        context,
                        "Recipient chat id is empty.");

                    return;
                }

                var botToken =
                    GetBotTokenFromConfiguration(
                        service);

                SendMessageToTelegram(
                    botToken,
                    chatId,
                    messageText,
                    tracer)
                    .GetAwaiter()
                    .GetResult();

                context.OutputParameters[Constants.Response] =
                    JObject.FromObject(new
                    {
                        success = true
                    }).ToString();

                tracer.Trace(
                    "Telegram message sent successfully.");
            }
            catch (Exception ex)
            {
                tracer.Trace(
                    $"Plugin Exception: {ex}");

                SetErrorResponse(
                    context,
                    ex.Message);
            }
        }

        private void SetErrorResponse(
            IPluginExecutionContext context,
            string errorMessage)
        {
            context.OutputParameters[Constants.Response] =
                JObject.FromObject(new
                {
                    success = false,
                    error = errorMessage
                }).ToString();
        }

        private string GetBotTokenFromConfiguration(
            IOrganizationService service)
        {
            var query = new QueryExpression(
                TelegramConstants.ConfigTableName)
            {
                ColumnSet = new ColumnSet(
                    TelegramConstants.TokenFieldName),

                TopCount = 1
            };

            var results = service.RetrieveMultiple(query);

            if (results.Entities.Count == 0)
            {
                throw new Exception(
                    "Telegram configuration not found.");
            }

            var token =
                results.Entities[0]
                    .GetAttributeValue<string>(
                        TelegramConstants.TokenFieldName);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception(
                    "Telegram bot token is empty.");
            }

            return token;
        }

        private async Task SendMessageToTelegram(
            string token,
            string chatId,
            string text,
            ITracingService tracer)
        {
            using (var client = new HttpClient())
            {
                var url =
                    $"https://api.telegram.org/bot{token}/sendMessage";

                var jsonBody = JObject.FromObject(new
                {
                    chat_id = chatId,
                    text = text
                }).ToString();

                var content = new StringContent(
                    jsonBody,
                    Encoding.UTF8,
                    "application/json");

                var response =
                    await client.PostAsync(url, content);

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                tracer.Trace(
                    $"Telegram Response: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Telegram API Error: {responseBody}");
                }
            }
        }
    }
}