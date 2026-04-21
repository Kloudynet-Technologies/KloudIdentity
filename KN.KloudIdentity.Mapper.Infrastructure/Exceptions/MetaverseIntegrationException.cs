namespace KN.KloudIdentity.Mapper.Infrastructure.Exceptions;

/// <summary>
/// Represents an error returned from or encountered while communicating with the metaverse integration service.
/// </summary>
public class MetaverseIntegrationException : Exception
{
    public MetaverseIntegrationException(string message)
        : base(message)
    {
    }

    public MetaverseIntegrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
