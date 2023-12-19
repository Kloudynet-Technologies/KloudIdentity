﻿using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperOverride;

public class CreateUser_Zoho : CreateUser
{
    public CreateUser_Zoho(IConfigReader configReader, IAuthContext authContext, IHttpClientFactory httpClientFactory) : base(configReader, authContext, httpClientFactory)
    {
    }

    public override Task MapAndPreparePayloadAsync()
    {
        Payload = new JObject
        {
            ["users"] = new JArray
            {
                new JObject
                {
                    ["first_name"] = Resource.Name.GivenName,
                    ["last_name"] = Resource.Name.FamilyName,
                    ["email"] = Resource.UserName,
                    ["role"] = "6049807000000026008",
                    ["profile"] = "6049807000000026014"
                }
            }
        };

        return Task.CompletedTask;
    }

    public override Task<string> GetAuthenticationAsync(AuthConfig config)
    {
        return Task.FromResult($"{config.Token}");
    }
}