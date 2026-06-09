namespace KN.KloudIdentity.Mapper.Domain.Entities.Shared;

public class EntityBase(DateTime createdDate, string createdBy, DateTime? modifiedDate, string? modifiedBy)
{
    public DateTime CreatedDate { get; set; } = createdDate;
    public DateTime? ModifiedDate { get; set; } = modifiedDate;
    public string CreatedBy { get; set; } = createdBy;
    public string? ModifiedBy { get; set; } = modifiedBy;
}