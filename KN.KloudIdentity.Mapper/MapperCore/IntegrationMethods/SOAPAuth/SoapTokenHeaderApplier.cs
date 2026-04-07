using System.Xml;

namespace KN.KloudIdentity.Mapper.MapperCore;

public sealed class SoapTokenHeaderApplier : ISoapAuthApplier
{
    private const string Soap11Ns = "http://schemas.xmlsoap.org/soap/envelope/";

    public Task ApplyAsync(SoapAuthContext context, CancellationToken cancellationToken = default)
    {
        var placement = context.AuthOptions?.TokenPlacement;
        if (placement?.Enabled != true || string.IsNullOrWhiteSpace(placement.SoapHeaderTemplate))
        {
            return Task.CompletedTask;
        }

        var tokenValue = context.Token ?? string.Empty;
        var tokenPlaceholder = placement.TokenPlaceholder;
        var headerFragment = placement.SoapHeaderTemplate.Replace(tokenPlaceholder, tokenValue, StringComparison.Ordinal);

        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(context.Payload);

        var envelope = document.DocumentElement;
        if (envelope == null || !envelope.LocalName.Equals("Envelope", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SOAP Envelope is required for custom SOAP header injection.");
        }

        var soapNs = string.IsNullOrWhiteSpace(envelope.NamespaceURI) ? Soap11Ns : envelope.NamespaceURI;
        var nsmgr = new XmlNamespaceManager(document.NameTable);
        nsmgr.AddNamespace("soap", soapNs);

        var header = document.SelectSingleNode("/soap:Envelope/soap:Header", nsmgr) as XmlElement;
        if (header == null)
        {
            header = document.CreateElement("soap", "Header", soapNs);
            var body = document.SelectSingleNode("/soap:Envelope/soap:Body", nsmgr);
            if (body?.ParentNode == null)
            {
                throw new InvalidOperationException("SOAP Body is required for custom SOAP header injection.");
            }

            body.ParentNode.InsertBefore(header, body);
        }

        var fragmentDocument = new XmlDocument { XmlResolver = null };
        fragmentDocument.LoadXml($"<root>{headerFragment}</root>");

        foreach (XmlNode child in fragmentDocument.DocumentElement!.ChildNodes)
        {
            var importedNode = document.ImportNode(child, true);
            header.AppendChild(importedNode);
        }

        context.Payload = document.OuterXml;
        return Task.CompletedTask;
    }
}
