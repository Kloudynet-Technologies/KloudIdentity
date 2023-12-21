using System.ComponentModel.DataAnnotations;

namespace KN.KloudIdentity.Mapper;

public class UserIdMapperModel
{
    public UserIdMapperModel(string identifier, string createdUserId, string appId)
    {
        Identifier = identifier;
        CreatedUserId = createdUserId;
        AppId = appId;
    }

    [Key]
    public string Identifier { get; private set; }

    public string CreatedUserId { get; private set; }

    public string AppId { get; private set; }
}
