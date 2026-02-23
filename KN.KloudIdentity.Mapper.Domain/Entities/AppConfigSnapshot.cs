using KN.KloudIdentity.Mapper.Domain.Entities.Shared;

namespace KN.KloudIdentity.Mapper.Domain.Entities;

public class AppConfigSnapshot(
    int id,
    string appId,
    string etag,
    string configJson,
    DateTime createdDate,
    string createdBy,
    DateTime? modifiedDate,
    string? modifiedBy)
    : EntityBase(createdDate, createdBy, modifiedDate, modifiedBy)
{
    public int Id { get; set; } = id;
    public string AppId { get; set; } = appId;
    public string Etag { get; set; } = etag;
    public string ConfigJson { get; set; } = configJson;
}