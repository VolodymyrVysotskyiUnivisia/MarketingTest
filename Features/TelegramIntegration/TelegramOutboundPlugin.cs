using MarketingTest.Base;
using MarketingTest.Shared;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MarketingTest.Features.TelegramIntegration
{
    // Order - 1
    // Message - Send
    // Description: Sends outbound message to Telegram with support for text and images using Clean Architecture and DTOs.
    public class TelegramOutboundPlugin : PluginBase
    {
        public TelegramOutboundPlugin() : base(typeof(TelegramOutboundPlugin))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localContext)
        {
            localContext.Trace("=== TelegramOutboundPlugin Started ===");

            IOrganizationService service = localContext.OrgSvcFactory.CreateOrganizationService(null);
            IPluginExecutionContext context = localContext.PluginExecutionContext;

            string channelDefinitionId = string.Empty;
            string requestId = string.Empty;

            try
            {
                if (!context.InputParameters.Contains(Constants.Payload))
                {
                    SetResponse(context, channelDefinitionId, requestId, Constants.StatusNotSent);
                    return;
                }

                string payloadJson = context.InputParameters[Constants.Payload]?.ToString();
                localContext.Trace($"Payload: {payloadJson}");

                if (string.IsNullOrWhiteSpace(payloadJson))
                {
                    SetResponse(context, channelDefinitionId, requestId, Constants.StatusNotSent);
                    return;
                }

                TelegramOutboundPayload payloadData;

                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(payloadJson)))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TelegramOutboundPayload));
                    payloadData = (TelegramOutboundPayload)serializer.ReadObject(ms);
                }

                channelDefinitionId = payloadData.ChannelDefinitionId ?? string.Empty;
                requestId = payloadData.RequestId ?? string.Empty;

                string to = payloadData.To ?? string.Empty;
                string from = payloadData.From ?? string.Empty;

                string text = payloadData.Message?.Text ?? string.Empty;
                string imageUrl = payloadData.Message?.Image ?? string.Empty;

                localContext.Trace($"Parsed values - To: {to}, From: {from}, HasImage: {!string.IsNullOrWhiteSpace(imageUrl)}");

                if (string.IsNullOrWhiteSpace(to) || (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(imageUrl)))
                {
                    SetResponse(context, channelDefinitionId, requestId, Constants.StatusNotSent);
                    return;
                }

                string botToken = GetBotTokenFromConfiguration(service, from);

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    SendPhotoMessage(botToken, to, text, imageUrl, localContext);
                }
                else
                {
                    SendTextMessage(botToken, to, text, localContext);
                }

                SetResponse(context, channelDefinitionId, requestId, Constants.StatusSent);
                localContext.Trace("Telegram message sent successfully.");
            }
            catch (Exception ex)
            {
                localContext.Trace($"Plugin Exception: {ex.Message}");
                SetResponse(context, channelDefinitionId, requestId, Constants.StatusNotSent);
            }
        }

        private string GetBotTokenFromConfiguration(IOrganizationService service, string from)
        {
            var query = new QueryExpression(TelegramConstants.ConfigTableName)
            {
                ColumnSet = new ColumnSet(TelegramConstants.TokenFieldName)
            };

            if (!string.IsNullOrWhiteSpace(from))
            {
                query.Criteria.AddCondition(TelegramConstants.NameFieldName, ConditionOperator.Equal, from);
            }

            EntityCollection results = service.RetrieveMultiple(query);

            if (results.Entities.Count == 0)
            {
                query.Criteria = new FilterExpression();
                query.TopCount = 1;
                results = service.RetrieveMultiple(query);
            }

            if (results.Entities.Count == 0)
            {
                throw new Exception("Telegram configuration not found.");
            }

            string token = results.Entities[0].GetAttributeValue<string>(TelegramConstants.TokenFieldName);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("Telegram bot token is empty.");
            }

            return token;
        }

        private void SendTextMessage(string token, string chatId, string text, ILocalPluginContext localContext)
        {
            localContext.Trace("Preparing Text Message for Telegram...");

            string url = $"{TelegramConstants.ApiUrlBase}{token}{TelegramConstants.SendMessageEndpoint}";
            string escapedText = EscapeJsonString(text);

            string requestBody = $"{{\"{TelegramConstants.ChatIdParam}\":\"{chatId}\",\"{TelegramConstants.TextParam}\":\"{escapedText}\"}}";

            ExecuteTelegramApiRequest(url, requestBody, localContext);
        }

        private void SendPhotoMessage(string token, string chatId, string text, string imageUrl, ILocalPluginContext localContext)
        {
            localContext.Trace("Preparing Photo Message for Telegram...");

            string url = $"{TelegramConstants.ApiUrlBase}{token}{TelegramConstants.SendPhotoEndpoint}";
            string escapedText = EscapeJsonString(text);
            string escapedImageUrl = EscapeJsonString(imageUrl);

            string requestBody = $"{{\"{TelegramConstants.ChatIdParam}\":\"{chatId}\",\"{TelegramConstants.PhotoParam}\":\"{escapedImageUrl}\",\"{TelegramConstants.CaptionParam}\":\"{escapedText}\"}}";

            ExecuteTelegramApiRequest(url, requestBody, localContext);
        }

        private void ExecuteTelegramApiRequest(string url, string requestBody, ILocalPluginContext localContext)
        {
            using (var client = new HttpClient())
            {
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                var response = client.PostAsync(url, content).GetAwaiter().GetResult();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                localContext.Trace($"Telegram API Response: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Telegram API Error: {responseBody}");
                }
            }
        }

        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "");
        }

        private void SetResponse(IPluginExecutionContext context, string channelDefinitionId, string requestId, string status)
        {
            context.OutputParameters[Constants.Response] =
                $"{{\"{TelegramConstants.ChannelDefinitionIdKey}\":\"{channelDefinitionId}\"," +
                $"\"{TelegramConstants.RequestIdKey}\":\"{requestId}\"," +
                $"\"{Constants.StatusKey}\":\"{status}\"}}";
        }
    }
}