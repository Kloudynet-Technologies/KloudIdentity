namespace KN.KloudIdentity.Mapper.Domain.Mapping;

public record MappingCondition
{
    public MappingConditions? Condition { get; init; }

    public string? SourceFieldName { get; init; }

    public AttributeDataTypes? SourceDataType { get; init; }
}
