//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    public interface IRemoveGroupMembers
    {
        Task RemoveAsync(string groupId, List<string> members, string appId, string correlationIdentifier);
    }
}
