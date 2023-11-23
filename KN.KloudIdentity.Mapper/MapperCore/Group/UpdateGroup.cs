//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    public class UpdateGroup : OperationsBase<Core2Group>, IUpdateResource<Core2Group>
    {
        public UpdateGroup(IConfigReader configReader, IAuthContext authContext) : base(configReader, authContext)
        {
        }

        public override Task MapAndPreparePayloadAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Resource> UpdateAsync(Resource resource, string appId, string correlationID)
        {
            throw new NotImplementedException();
        }
    }
}
