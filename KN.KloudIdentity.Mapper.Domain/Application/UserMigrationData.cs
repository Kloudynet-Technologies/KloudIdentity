namespace KN.KloudIdentity.Mapper.Domain.Application;

public record UserMigrationData(
    string PartitionKey,
    string RowKey,
    string CorrelationId
);