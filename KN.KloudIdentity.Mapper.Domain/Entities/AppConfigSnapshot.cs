using KN.KloudIdentity.Mapper.Domain.Entities.Shared;

namespace KN.KloudIdentity.Mapper.Domain.Entities;

public class AppConfigSnapshot : EntityBase
{
    // EF Core needs a parameterless constructor
    // Base args can be dummy; EF will hydrate properties after construction
    private AppConfigSnapshot() : base(default, string.Empty, null, null)
    {
    }

    public AppConfigSnapshot(
        int id,
        string appId,
        string etag,
        string configJson,
        DateTime generatedDate,
        DateTime createdDate,
        string createdBy,
        DateTime? modifiedDate,
        string? modifiedBy)
        : base(createdDate, createdBy, modifiedDate, modifiedBy)
    {
        Id = id;
        AppId = appId;
        Etag = etag;
        ConfigJson = configJson;
        GeneratedDate = generatedDate;
    }

    public int Id { get; private set; }                 // PK
    public string AppId { get; private set; } = default!;
    public string Etag { get; private set; } = default!;
    public string ConfigJson { get; private set; } = default!;
    public DateTime GeneratedDate { get; private set; }

    public void UpdateSnapshot(string etag, string configJson, DateTime generatedDate, string modifiedBy)
    {
        Etag = etag;
        ConfigJson = configJson;
        GeneratedDate = generatedDate;

        ModifiedBy = modifiedBy;
        ModifiedDate = DateTime.UtcNow;
    }
}