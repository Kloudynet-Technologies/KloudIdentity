//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    public interface IRemoveAllGroupMembers
    {
        Task RemoveAsync(string groupId, string appId, string correlationID);
    }
}
