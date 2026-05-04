using System.Xml;

namespace KN.KloudIdentity.Mapper.MapperCore;

public sealed class WsSecuritySoapAuthApplier : ISoapAuthApplier
{
    private const string Soap11Ns = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string WsseNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string WsuNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    public Task ApplyAsync(SoapAuthContext context, CancellationToken cancellationToken = default)
    {
        var wsSecurity = context.AuthOptions?.WsSecurity;
        if (wsSecurity?.Enabled != true)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(wsSecurity.Username) || string.IsNullOrWhiteSpace(wsSecurity.Password))
        {
            throw new InvalidOperationException("WS-Security UsernameToken requires both username and password.");
        }

        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(context.Payload);

        var envelope = document.DocumentElement;
        if (envelope == null || !envelope.LocalName.Equals("Envelope", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SOAP Envelope is required for WS-Security injection.");
        }

        var soapNs = string.IsNullOrWhiteSpace(envelope.NamespaceURI) ? Soap11Ns : envelope.NamespaceURI;
        var nsmgr = new XmlNamespaceManager(document.NameTable);
        nsmgr.AddNamespace("soap", soapNs);
        nsmgr.AddNamespace("wsse", WsseNs);

        var header = document.SelectSingleNode("/soap:Envelope/soap:Header", nsmgr) as XmlElement;
        if (header == null)
        {
            header = document.CreateElement("soap", "Header", soapNs);
            var body = document.SelectSingleNode("/soap:Envelope/soap:Body", nsmgr);
            if (body?.ParentNode == null)
            {
                throw new InvalidOperationException("SOAP Body is required for WS-Security injection.");
            }

            body.ParentNode.InsertBefore(header, body);
        }

        var security = header.SelectSingleNode("wsse:Security", nsmgr) as XmlElement;
        if (security == null)
        {
            security = document.CreateElement("wsse", "Security", WsseNs);
            header.AppendChild(security);
        }

        var usernameToken = document.CreateElement("wsse", "UsernameToken", WsseNs);

        var username = document.CreateElement("wsse", "Username", WsseNs);
        username.InnerText = wsSecurity.Username;
        usernameToken.AppendChild(username);

        var password = document.CreateElement("wsse", "Password", WsseNs);
        password.InnerText = wsSecurity.Password;
        usernameToken.AppendChild(password);

        if (wsSecurity.IncludeTimestamp)
        {
            var timestamp = document.CreateElement("wsu", "Timestamp", WsuNs);
            var created = document.CreateElement("wsu", "Created", WsuNs);
            var expires = document.CreateElement("wsu", "Expires", WsuNs);
            var utcNow = DateTime.UtcNow;

            created.InnerText = utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            expires.InnerText = utcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            timestamp.AppendChild(created);
            timestamp.AppendChild(expires);
            security.AppendChild(timestamp);
        }

        security.AppendChild(usernameToken);

        context.Payload = document.OuterXml;
        return Task.CompletedTask;
    }
}
