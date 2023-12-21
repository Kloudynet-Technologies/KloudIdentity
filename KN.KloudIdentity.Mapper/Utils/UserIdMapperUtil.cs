using KN.KloudIdentity.Mapper.Config.Db;
using Microsoft.EntityFrameworkCore;

namespace KN.KloudIdentity.Mapper;

public class UserIdMapperUtil
{
    private readonly Context _context;

    public UserIdMapperUtil(Context context)
    {
        _context = context;
    }

    public string? GetCreatedUserId(string identifier, string appId)
    {
        var userIdMapper = _context.UserIdMap.FirstOrDefault(x => x.Identifier == identifier && x.AppId == appId);
        return userIdMapper?.CreatedUserId;
    }

    public void AddUserIdMapper(string identifier, string createdUserId, string appId)
    {
        var userIdMapper = new UserIdMapperModel(identifier, createdUserId, appId);
        _context.UserIdMap.Add(userIdMapper);
        _context.SaveChanges();
    }
}
