//------------------------------------------------------------
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
                PUTAPIForGroups = appConfig.PUTAPIForGroups,
                DELETEAPIForGroups = appConfig.DELETEAPIForGroups,
                GETAPIForGroups = appConfig.GETAPIForGroups,
                LISTAPIForGroups = appConfig.LISTAPIForGroups,
                PATCHAPIForGroups = appConfig.PATCHAPIForGroups,
                PATCHAPIForAddMemberToGroup = appConfig.PATCHAPIForAddMemberToGroup,
                PATCHAPIForRemoveMemberFromGroup = appConfig.PATCHAPIForRemoveMemberFromGroup,
                PATCHAPIForRemoveAllMembersFromGroup = appConfig.PATCHAPIForRemoveAllMembersFromGroup,
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
                UserSchema = TransformToUserSchema(appConfig.UserSchema),
                GroupSchema = TransformToGroupSchema(appConfig.GroupSchema)
            };
        }

        public static List<SchemaAttribute> TransformToGroupSchema(
            this ICollection<GroupSchemaModel> groupSchemaModels
        )
        {
            return groupSchemaModels
                .Select(
                    x =>
                        new SchemaAttribute
                        {
                            DataType = (JSonDataType)x.DataType,
                            FieldName = x.FieldName,
                            IsRequired = x.IsRequired,
                            MappedAttribute = x.MappedAttribute,
                            ArrayElementMappingField = x.ArrayElementMappingField,
                            ArrayElementType = x.ArrayElementType,
                            ChildSchemas = x.ChildSchemas?.TransformToGroupSchema()
                        }
                )
                .ToList();
        }

        public static List<SchemaAttribute> TransformToUserSchema(
            this ICollection<UserSchemaModel> userSchemaModels
        )
        {
            return userSchemaModels
                .Select(
                    x =>
                        new SchemaAttribute
                        {
                            DataType = (JSonDataType)x.DataType,
                            FieldName = x.FieldName,
                            IsRequired = x.IsRequired,
                            MappedAttribute = x.MappedAttribute,
                            ArrayElementMappingField = x.ArrayElementMappingField,
                            ArrayElementType = x.ArrayElementType,
                            ChildSchemas = x.ChildSchemas?.TransformToUserSchema()
                        }
                )
                .ToList();
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
                PUTAPIForGroups = config.PUTAPIForGroups,
                DELETEAPIForGroups = config.DELETEAPIForGroups,
                GETAPIForGroups = config.GETAPIForGroups,
                LISTAPIForGroups = config.LISTAPIForGroups,
                PATCHAPIForGroups = config.PATCHAPIForGroups,
                PATCHAPIForAddMemberToGroup = config.PATCHAPIForAddMemberToGroup,
                PATCHAPIForRemoveMemberFromGroup = config.PATCHAPIForRemoveMemberFromGroup,
                PATCHAPIForRemoveAllMembersFromGroup = config.PATCHAPIForRemoveAllMembersFromGroup,

                AuthConfig = TransformToAuthConfigModel(
                    appId: config.AppId,
                    authConfig: config.AuthConfig
                ),
                GroupSchema = TransformToGroupSchemaModel(
                    appId: config.AppId,
                    schemaAttributes: config.GroupSchema
                ),
                UserSchema = TransformToUserSchemaModel(
                    appId: config.AppId,
                    schemaAttributes: config.UserSchema
                )
            };

            return configModel;
        }

        public static AuthConfigModel TransformToAuthConfigModel(
            this AuthConfig authConfig,
            string appId
        )
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

        public static List<GroupSchemaModel> TransformToGroupSchemaModel(
            this IList<SchemaAttribute> schemaAttributes,
            string appId
        )
        {
            var groupSchemaModels = schemaAttributes
                .Select(
                    x =>
                        new GroupSchemaModel
                        {
                            AppId = appId,
                            DataType = (int)x.DataType,
                            FieldName = x.FieldName,
                            IsRequired = x.IsRequired,
                            MappedAttribute = x.MappedAttribute,
                            ArrayElementMappingField = x.ArrayElementMappingField,
                            ArrayElementType = x.ArrayElementType,
                            ChildSchemas = x.ChildSchemas?.TransformToGroupSchemaModel(appId)
                        }
                )
                .ToList();

            return groupSchemaModels;
        }

        public static List<UserSchemaModel> TransformToUserSchemaModel(
            this IList<SchemaAttribute> schemaAttributes,
            string appId
        )
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
                            MappedAttribute = x.MappedAttribute,
                            ArrayElementMappingField = x.ArrayElementMappingField,
                            ArrayElementType = x.ArrayElementType,
                            ChildSchemas = x.ChildSchemas?.TransformToUserSchemaModel(appId)
                        }
                )
                .ToList();
        }
    }
}
