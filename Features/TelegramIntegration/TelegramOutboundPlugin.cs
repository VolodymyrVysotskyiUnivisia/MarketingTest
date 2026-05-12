using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Net.Http;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;

namespace MarketingTest.Features.TelegramIntegration
{
    public class TelegramOutboundPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(null);
            var tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            string channelDefId = "";
            string reqId = "";

            try
            {
                tracer.Trace("=== TelegramOutboundPlugin Native Execution Started ===");

                if (!context.InputParameters.Contains("payload"))
                {
                    context.OutputParameters["response"] = "{\"Status\":\"NotSent\"}";
                    return;
                }

                string payloadString = context.InputParameters["payload"]?.ToString();
                tracer.Trace("Payload: " + payloadString);

                // ПАРСИНГ БЕЗ NEWTONSOFT.JSON (Використовуємо нативний вбудований парсер Dataverse)
                var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(payloadString), new System.Xml.XmlDictionaryReaderQuotas());
                var xml = XElement.Load(reader);

                channelDefId = xml.Element("ChannelDefinitionId")?.Value ?? "";
                reqId = xml.Element("RequestId")?.Value ?? "";
                string to = xml.Element("To")?.Value ?? "";
                string fromText = xml.Element("From")?.Value ?? "";
                string text = xml.Element("Message")?.Element("text")?.Value ?? "";

                tracer.Trace($"Parsed values - To: {to}, From: {fromText}, ReqId: {reqId}");

                // 1. Шукаємо токен бота
                string botToken = "";
                var query = new QueryExpression("tg_telegramconfiguration") { ColumnSet = new ColumnSet("tg_bottoken") };

                if (!string.IsNullOrWhiteSpace(fromText))
                {
                    query.Criteria.AddCondition("tg_name", ConditionOperator.Equal, fromText);
                }

                var results = service.RetrieveMultiple(query);
                if (results.Entities.Count > 0)
                {
                    botToken = results.Entities[0].GetAttributeValue<string>("tg_bottoken");
                }
                else
                {
                    var fallbackQuery = new QueryExpression("tg_telegramconfiguration") { ColumnSet = new ColumnSet("tg_bottoken"), TopCount = 1 };
                    var fallbackResults = service.RetrieveMultiple(fallbackQuery);
                    if (fallbackResults.Entities.Count > 0)
                    {
                        botToken = fallbackResults.Entities[0].GetAttributeValue<string>("tg_bottoken");
                    }
                }

                if (string.IsNullOrWhiteSpace(botToken))
                {
                    throw new Exception("Bot token not found in Dataverse.");
                }

                // 2. Відправка в Telegram (збираємо JSON вручну)
                using (var client = new HttpClient())
                {
                    var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

                    // Екрануємо текст, щоб він не зламав JSON (особливо перенесення рядків, як у твоєму тестовому повідомленні)
                    string escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                    string bodyStr = $"{{\"chat_id\":\"{to}\",\"text\":\"{escapedText}\"}}";

                    var content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
                    var tgResponse = client.PostAsync(url, content).GetAwaiter().GetResult();
                    var tgResponseBody = tgResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    tracer.Trace("Telegram response: " + tgResponseBody);
                }

                // 3. Формуємо успішну відповідь
                string successResponse = $"{{\"ChannelDefinitionId\":\"{channelDefId}\",\"RequestId\":\"{reqId}\",\"Status\":\"Sent\"}}";
                context.OutputParameters["response"] = successResponse;

                tracer.Trace("Plugin executed successfully.");
            }
            catch (Exception ex)
            {
                tracer.Trace("EXCEPTION CAUGHT: " + ex.ToString());
                string errorResponse = $"{{\"ChannelDefinitionId\":\"{channelDefId}\",\"RequestId\":\"{reqId}\",\"Status\":\"NotSent\"}}";
                context.OutputParameters["response"] = errorResponse;
            }
        }
    }
}