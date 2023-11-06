using KN.KloudIdentity.Mapper.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Config.Helper
{
    public class ConfigReaderHelper
    {
        public static MapperConfig FormatMapperConfig(AppConfigModel appConfig)
        {
            return new MapperConfig
            {
                AppId = appConfig.AppId,
                UserProvisioningApiUrl = appConfig.UserProvisioningApiUrl,
                GroupProvisioningApiUrl = appConfig.GroupProvisioningApiUrl,
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
                    Username = appConfig.AuthConfig.Username
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

        public static AppConfigModel FormatAppConfigModel(MapperConfig config)
        {
            var configModel = new AppConfigModel
            {
                AppId = config.AppId,
                UserProvisioningApiUrl = config.UserProvisioningApiUrl,
                GroupProvisioningApiUrl = config.GroupProvisioningApiUrl,
                AuthConfig = FormatAuthConfigModel(
                    appId: config.AppId,
                    authConfig: config.AuthConfig
                ),
                GroupSchema = FormatGroupSchemaModel(
                    appId: config.AppId,
                    schemaAttributes: config.GroupSchema
                ),
                UserSchema = FormatUserSchemaModel(config.AppId, config.UserSchema)
            };

            return configModel;
        }

        public static AuthConfigModel FormatAuthConfigModel(string appId, AuthConfig authConfig)
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
                Username = authConfig.Username
            };
        }

        public static List<GroupSchemaModel> FormatGroupSchemaModel(
            string appId,
            IList<SchemaAttribute> schemaAttributes
        )
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

        public static List<UserSchemaModel> FormatUserSchemaModel(
            string appId,
            IList<SchemaAttribute> schemaAttributes
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
                            MappedAttribute = x.MappedAttribute
                        }
                )
                .ToList();
        }
    }
}
