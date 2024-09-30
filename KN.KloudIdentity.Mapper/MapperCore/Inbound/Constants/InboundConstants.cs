using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public static class InboundConstants
{
    public static readonly IList<string> MANDATORY_ATTRIBUTES =
    [
        "externalId",
        "preferredLanguage",
        "userId",
        "firstName",
        "lastName",
        "userPrincipalName",
        "mailNickName",
        "isActive"
    ];

    public const string SCIM_EXTENSION_SCHEMA = "urn:ietf:params:scim:schemas:extension:ki:1.0:User";

    public const string BULKREQUEST_SCHEMA = "urn:ietf:params:scim:api:messages:2.0:BulkRequest";

    public static readonly IList<JsonDataTypes> VALID_DATA_TYPES = new List<JsonDataTypes>
    {
        JsonDataTypes.String,
        JsonDataTypes.Boolean,
        JsonDataTypes.Number,
        JsonDataTypes.DateTime
    };

    public static readonly IList<MappingTypes> VALID_MAPPING_TYPES = new List<MappingTypes>
    {
        MappingTypes.Direct,
        MappingTypes.Constant
    };

    public const string SCIM_EXTENSION_TEMPLATE = @"
    {
        ""schemas"": [""urn:ietf:params:scim:api:messages:2.0:BulkRequest""],
        ""Operations"" : [],
        ""failOnErrors"": null
    }";

    public const string SCIM_USER_TEMPLATE = @"{
                ""method"": ""POST"",
                ""bulkId"": """",
                ""path"": ""/Users"",
                ""data"":
                    {
                    ""schemas"": [""urn:ietf:params:scim:schemas:core:2.0:User"",
                            ""urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"",
                            ""urn:ietf:params:scim:schemas:extension:ki:1.0:User""],
                    ""externalId"": """",
                    ""preferredLanguage"": """",
                    ""urn:ietf:params:scim:schemas:extension:ki:1.0:User"": {
                        ""userId"": """",
                        ""firstName"": """",
                        ""lastName"": """",
                        ""email"": """",
                        ""departmentId"": """",
                        ""isActive"": false
                        }
                    }
            }";

    public const string SCIM_USER_EXTENSION_SCHEMA = "urn:ietf:params:scim:schemas:extension:ki:1.0:User";
}
