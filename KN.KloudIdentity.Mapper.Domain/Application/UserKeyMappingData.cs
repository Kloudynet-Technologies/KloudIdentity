namespace KN.KloudIdentity.Mapper.Domain.Application;

public record UserKeyMappingData(
    string PartitionKey,
    string RowKey,
    string UserKey,
    string Username
);