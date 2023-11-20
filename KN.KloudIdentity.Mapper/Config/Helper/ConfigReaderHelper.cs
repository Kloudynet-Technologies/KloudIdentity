﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;

namespace KN.KloudIdentity.Mapper.Config.Helper
{
    public static class ConfigReaderExtensions
    {
        public static MapperConfig TransformToMapperConfig(this AppConfigModel appConfig)
        {
            return new MapperConfig
            {
                AppId = appConfig.AppId,
                UserProvisioningApiUrl = appConfig.UserProvisioningApiUrl,
                GroupProvisioningApiUrl = appConfig.GroupProvisioningApiUrl,
                DELETEAPIForUsers = appConfig.DELETEAPIForUsers,
                GETAPIForUsers = appConfig.GETAPIForUsers,
                LISTAPIForUsers = appConfig.LISTAPIForUsers,
                PATCHAPIForUsers = appConfig.PATCHAPIForUsers,
                PUTAPIForUsers = appConfig.PUTAPIForUsers,
                AuthConfig = new AuthConfig
                {
                    AuthenticationMethod = (AuthenticationMethod)
                        appConfig.AuthConfig.AuthenticationMethod,
                    ClientId = appConfig.AuthConfig.ClientId,
                    ClientSecret = appConfig.AuthConfig.ClientSecret,
                    Authority = appConfig.AuthConfig.Authority,
                    Password = appConfig.AuthConfig.Password,
                    RedirectUri = appConfig.AuthConfig.RedirectUri,
                    Scope = appConfig.AuthConfig.Scope,
                    Token = appConfig.AuthConfig.Token,
                    Username = appConfig.AuthConfig.Username,
                    OAuth2TokenUrl = appConfig.AuthConfig.OAuth2TokenUrl,
                    ApiKey = appConfig.AuthConfig.ApiKey,
                    ApiKeyHeader = appConfig.AuthConfig.ApiKeyHeader
                },
                UserSchema = appConfig.UserSchema
                    .Select(
                        x =>
                            new SchemaAttribute
                            {
                                DataType = (JSonDataType)x.DataType,
                                FieldName = x.FieldName,
                                IsRequired = x.IsRequired,
                                MappedAttribute = x.MappedAttribute
                            }
                    )
                    .ToList(),
                GroupSchema = appConfig.GroupSchema
                    .Select(
                        x =>
                            new SchemaAttribute
                            {
                                DataType = (JSonDataType)x.DataType,
                                FieldName = x.FieldName,
                                IsRequired = x.IsRequired,
                                MappedAttribute = x.MappedAttribute
                            }
                    )
                    .ToList()
            };
        }

        public static AppConfigModel TransformToAppConfigModel(this MapperConfig config)
        {
            var configModel = new AppConfigModel
            {
                AppId = config.AppId,
                UserProvisioningApiUrl = config.UserProvisioningApiUrl,
                GroupProvisioningApiUrl = config.GroupProvisioningApiUrl, 
                DELETEAPIForUsers = config.DELETEAPIForUsers,
                GETAPIForUsers = config.GETAPIForUsers,
                LISTAPIForUsers = config.LISTAPIForUsers,
                PATCHAPIForUsers = config.PATCHAPIForUsers,
                PUTAPIForUsers = config.PUTAPIForUsers,
                AuthConfig = TransformToAuthConfigModel(
                    appId: config.AppId,
                    authConfig: config.AuthConfig
                ),
                GroupSchema = TransformToGroupSchemaModel(
                    appId: config.AppId,
                    schemaAttributes: config.GroupSchema
                ),
                UserSchema = TransformToUserSchemaModel(appId: config.AppId, schemaAttributes: config.UserSchema)
            };

            return configModel;
        }

        public static AuthConfigModel TransformToAuthConfigModel(this AuthConfig authConfig, string appId)
        {
            return new AuthConfigModel
            {
                AppId = appId,
                AuthenticationMethod = (int)authConfig.AuthenticationMethod,
                ClientId = authConfig.ClientId,
                ClientSecret = authConfig.ClientSecret,
                Authority = authConfig.Authority,
                Password = authConfig.Password,
                RedirectUri = authConfig.RedirectUri,
                Scope = authConfig.Scope,
                Token = authConfig.Token,
                Username = authConfig.Username,
                OAuth2TokenUrl = authConfig.OAuth2TokenUrl,
                ApiKey = authConfig.ApiKey,
                ApiKeyHeader = authConfig.ApiKeyHeader
            };
        }

        public static List<GroupSchemaModel> TransformToGroupSchemaModel(this IList<SchemaAttribute> schemaAttributes, string appId)
        {
            return schemaAttributes
                .Select(
                    x =>
                        new GroupSchemaModel
                        {
                            AppId = appId,
                            DataType = (int)x.DataType,
                            FieldName = x.FieldName,
                            IsRequired = x.IsRequired,
                            MappedAttribute = x.MappedAttribute
                        }
                )
                .ToList();
        }

        public static List<UserSchemaModel> TransformToUserSchemaModel(this IList<SchemaAttribute> schemaAttributes, string appId)
        {
            return schemaAttributes
                .Select(
                    x =>
                        new UserSchemaModel
                        {
                            AppId = appId,
                            DataType = (int)x.DataType,
                            FieldName = x.FieldName,
                            IsRequired = x.IsRequired,
                            MappedAttribute = x.MappedAttribute
                        }
                )
                .ToList();
        }
    }
}
