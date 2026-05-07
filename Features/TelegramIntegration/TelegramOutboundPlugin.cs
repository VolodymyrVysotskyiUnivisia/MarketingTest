using MarketingTest.Base;
using MarketingTest.Shared;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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

            var service = serviceFactory.CreateOrganizationService(null);

            if (context.InputParameters.Contains(Constants.Payload))
            {
                try
                {
                    tracer.Trace("Start of TelegramOutboundPlugin - {0}",
                        DateTime.UtcNow);

                    var payload = (string)context
                        .InputParameters[Constants.Payload];
                    tracer.Trace($"Payload received: {payload}");

                    var messageText = ExtractValue(
                        payload,
                        TelegramConstants.TextKey);
                    var chatId = ExtractValue(
                        payload,
                        TelegramConstants.ToKey);

                    tracer.Trace(
                        $"Parsed Text: '{messageText}', ChatID: '{chatId}'");

                    if (!string.IsNullOrEmpty(messageText)
                        && !string.IsNullOrEmpty(chatId))
                    {
                        var botToken = GetBotTokenFromConfiguration(
                            service,
                            tracer);

                        SendMessageToTelegram(
                            botToken,
                            chatId,
                            messageText)
                            .GetAwaiter()
                            .GetResult();

                        tracer.Trace("Message sent to Telegram successfully.");
                    }

                    context.OutputParameters[Constants.Response] = "{}";

                    tracer.Trace("End of TelegramOutboundPlugin - {0}",
                        DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    tracer.Trace("Plugin Exception: {0}", ex.ToString());

                    var safeError = ex.Message.Replace("\"", "'");
                    context.OutputParameters[Constants.Response] =
                        $"{{\"Error\": \"{safeError}\"}}";

                    throw new InvalidPluginExecutionException(
                        Constants.UnexpectedErrorMassage);
                }
            }
        }

        private string GetBotTokenFromConfiguration(
            IOrganizationService service,
            ITracingService tracer)
        {
            tracer.Trace("Fetching bot token from configuration...");

            var query = new QueryExpression(TelegramConstants.ConfigTableName)
            {
                ColumnSet = new ColumnSet(TelegramConstants.TokenFieldName),
                TopCount = 1
            };

            var results = service.RetrieveMultiple(query);

            if (results.Entities.Count > 0)
            {
                var token = results.Entities[0].GetAttributeValue<string>(
                    TelegramConstants.TokenFieldName);

                if (string.IsNullOrEmpty(token))
                {
                    throw new Exception("Bot token field is empty.");
                }

                tracer.Trace("Token successfully fetched.");

                return token;
            }

            throw new Exception("No Telegram configuration record found.");
        }

        private string ExtractValue(string json, string key)
        {
            var match = Regex.Match(
                json,
                $"\"{key}\"\\s*:\\s*\"(.*?)\"",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private async Task SendMessageToTelegram(
            string token,
            string chatId,
            string text)
        {
            using (var client = new HttpClient())
            {
                var url = $"https://api.telegram.org/bot{token}/sendMessage";

                var escapedText = text.Replace("\n", "\\n")
                    .Replace("\"", "\\\"");

                var jsonBody =
                    $"{{\"chat_id\": \"{chatId}\", \"text\": \"{escapedText}\"}}";

                var content = new StringContent(
                    jsonBody,
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetail = await response.Content
                        .ReadAsStringAsync();

                    throw new Exception(
                        $"HTTP {response.StatusCode} - {errorDetail}");
                }
            }
        }
    }
}