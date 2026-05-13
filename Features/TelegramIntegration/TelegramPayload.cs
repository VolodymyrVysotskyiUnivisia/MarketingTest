using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MarketingTest.Features.TelegramIntegration
{
    [DataContract]
    public class TelegramOutboundPayload
    {
        [DataMember(Name = "ChannelDefinitionId")]
        public string ChannelDefinitionId { get; set; }

        [DataMember(Name = "RequestId")]
        public string RequestId { get; set; }

        [DataMember(Name = "To")]
        public string To { get; set; }

        [DataMember(Name = "From")]
        public string From { get; set; }

        [DataMember(Name = "Message")]
        public TelegramOutboundMessage Message { get; set; }
    }

    [DataContract]
    public class TelegramOutboundMessage
    {
        [DataMember(Name = "text")]
        public string Text { get; set; }

        [DataMember(Name = "image")]
        public string Image { get; set; }
    }
}