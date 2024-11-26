﻿using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IGetVerifiedAttributeMapping
{
    Task<JObject> GetVerifiedAsync(string appId,string correlationId, ObjectTypes type, HttpRequestTypes httpRequestType);
}
