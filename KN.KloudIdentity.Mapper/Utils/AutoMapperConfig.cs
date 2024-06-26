﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using AutoMapper;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper.Utils;

/// <summary>
/// Auto mapper config.
/// </summary>
public class AutoMapperConfig
{
    /// <summary>
    /// Initializes the automatic mapper.
    /// </summary>
    /// <returns></returns>
    public static AutoMapper.Mapper InitializeAutoMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<MapperConfig, AppConfigModel>();
            cfg.CreateMap<AuthConfig, AuthConfigModel>();
            cfg.CreateMap<SchemaAttribute, UserSchemaModel>();
            cfg.CreateMap<SchemaAttribute, GroupSchemaModel>();

            cfg.CreateMap<AppConfigModel, MapperConfig>();
        });

        return new AutoMapper.Mapper(config);
    }
}
