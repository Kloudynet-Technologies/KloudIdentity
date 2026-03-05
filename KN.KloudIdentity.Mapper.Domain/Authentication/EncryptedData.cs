namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public class EncryptedData
{
    public string EncryptedValue { get; set; } = string.Empty;

    public string IV { get; set; } = string.Empty;
}