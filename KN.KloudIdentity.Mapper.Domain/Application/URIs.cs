using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Application;

public record URIs
{
    /// <summary>
    /// Record Id
    /// </summary>
    public Guid RecordId { get; init; }

    /// <summary>
    /// Application Id
    /// </summary>
    public string AppId { get; init; }

    /// <summary>
    /// Base URL
    /// </summary>
    public string BaseUrl { get; init; }

    /// <summary>
    /// Post URI
    /// </summary>
    public Uri Post { get; init; }

    /// <summary>
    /// Get URI
    /// </summary>
    public Uri Get { get; init; }

    /// <summary>
    /// Put URI
    /// </summary>
    public Uri? Put { get; init; }

    /// <summary>
    /// Delete URI
    /// </summary>
    public Uri? Delete { get; init; }

    /// <summary>
    /// Patch URI
    /// </summary>
    public Uri? Patch { get; init; }

    /// <summary>
    /// List URI
    /// </summary>
    public Uri? List { get; init; }

    /// <summary>
    /// SCIM Direction Inbound or Outbound
    /// </summary>
    public SCIMDirections SCIMDirection { get; init; }
}
