using KN.KloudIdentity.Common.Enum;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class GetVerifiedAttributeMapping : IGetVerifiedAttributeMapping
{
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;

    public GetVerifiedAttributeMapping(IGetFullAppConfigQuery getFullAppConfigQuery)
    {
        _getFullAppConfigQuery = getFullAppConfigQuery;
    }
    public async Task<dynamic> GetVerifiedAsync(string appId, MappingType type, SCIMDirections direction, HttpRequestTypes httpRequestType)
    {
        if (direction == SCIMDirections.Inbound)
        {
            return new
            {
                message = "Inbound synchronization is not yet implemented."
            };
        }

        var appConfig = await _getFullAppConfigQuery.GetAsync(appId);

        if (appConfig == null)
            throw new NotFoundException("Application not found");

        var attributes = type == MappingType.Group ? appConfig.GroupAttributeSchemas : appConfig.UserAttributeSchemas;
        var filteredAttributes = attributes?.Where(x => x.HttpRequestType == httpRequestType && x.SCIMDirection == direction).ToList();

        return JSONParserUtilV2<Resource>.Parse(filteredAttributes, type == MappingType.Group ? GetDemoGroupData() : GetDemoUserData());
    }

    private Core2EnterpriseUser GetDemoUserData()
    {
        return new Core2EnterpriseUser
        {
            UserName = "string",
            DisplayName = "string",
            Active = true,
            Addresses = new List<Address>
            {
                new Address
                {
                    Country = "string",
                    Formatted = "string",
                    Locality = "string",
                    PostalCode = "string",
                    Region = "string",
                    StreetAddress = "string",
                    Primary = true,
                    ItemType = "string"
                },
                new Address
                {
                    Country = "string",
                    Formatted = "string",
                    Locality = "string",
                    PostalCode = "string",
                    Region = "string",
                    StreetAddress = "string",
                    Primary = false,
                    ItemType = "string"
                }
            },
            Name = new Name
            {
                FamilyName = "string",
                Formatted = "string",
                GivenName = "string",
                HonorificPrefix = "string",
                HonorificSuffix = "string"
            },

            Nickname = "string",
            ElectronicMailAddresses = new List<ElectronicMailAddress>
            {
                new ElectronicMailAddress
                {
                    Value = "string",
                    Primary = true,
                    ItemType = "string"
                },
                new ElectronicMailAddress
                {
                    Value = "string",
                    Primary = false,
                    ItemType = "string"
                }
            },
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                EmployeeNumber = "001",
                CostCenter = "string",
                Division = "string",
                Department = "string",
                Organization = "string",
                Manager = new Manager
                {
                    Value = "string"
                }
            },

            Identifier = "000",
            Title = "string",
            UserType = "string",
            ExternalIdentifier = "001",
            Locale = "string",
            PreferredLanguage = "string",
            TimeZone = "string",
            Metadata = new Core2Metadata
            {
                ResourceType = "string",
                Created = DateTime.Now,
                LastModified = DateTime.Now,
                Location = "string",
                Version = "string"
            },
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber
                {
                    Value = "string",
                    Primary = true,
                    ItemType = "string"
                },
                new PhoneNumber
                {
                    Value = "string",
                    Primary = false,
                    ItemType = "string"
                }
            },
            Roles = new List<Role>
            {
                new Role                {
                    Value = "string",
                    Display = "string",
                    Primary = true,
                    ItemType = "string"
                },
                new Role
                {
                    Value = "string",
                    Display = "string",
                    Primary = false,
                    ItemType = "string"
                }
            }

        };
    }

    private Core2Group GetDemoGroupData()
    {
        return new Core2Group
        {
            DisplayName = "string",
            Members = new List<Member>
            {
                new Member
                {
                    Value = "string",
                    TypeName = "string"
                }
            },
            ExternalIdentifier = "string",
            Identifier = "string",
            Metadata = new Core2Metadata
            {
                ResourceType = "string",
                Created = DateTime.Now,
                LastModified = DateTime.Now,
                Location = "string",
                Version = "string"
            }
        };
    }
}
