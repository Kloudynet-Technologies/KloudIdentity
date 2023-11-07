//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public interface IConfigReader
{
    Task<MapperConfig> GetConfigAsync(string appId, CancellationToken cancellationToken);

    Task CreateConfigAsync(MapperConfig config, CancellationToken cancellationToken);

    Task UpdateConfigAsync(MapperConfig config, CancellationToken cancellationToken);
}
