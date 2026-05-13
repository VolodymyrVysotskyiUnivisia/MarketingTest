using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketingTest.Features.TelegramIntegration
{
    public static class TelegramConstants
    {
        public const string ConfigTableName = "tg_telegramconfiguration";
        public const string TokenFieldName = "tg_bottoken";
        public const string NameFieldName = "tg_name";

        public const string ChannelDefinitionIdKey = "ChannelDefinitionId";
        public const string RequestIdKey = "RequestId";
        public const string ToKey = "To";
        public const string FromKey = "From";
        public const string MessageKey = "Message";
        public const string TextKey = "text";
        public const string ImageKey = "image";

        public const string ApiUrlBase = "https://api.telegram.org/bot";
        public const string SendMessageEndpoint = "/sendMessage";
        public const string SendPhotoEndpoint = "/sendPhoto";

        public const string ChatIdParam = "chat_id";
        public const string TextParam = "text";
        public const string PhotoParam = "photo";
        public const string CaptionParam = "caption";
    }
}