using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Infrastructure.Messaging
{
    public class AppMessage
    {
        public string AppId { get; set; }
        public MessageType MessageType { get; set; }
    }

    public enum MessageType
    { 
        none,
        GetFullApplication,
        GetApplicationSetting,
        ListInboundApplications,
    }
}
