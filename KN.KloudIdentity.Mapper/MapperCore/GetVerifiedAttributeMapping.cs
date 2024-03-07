using KN.KloudIdentity.Common.Enumr;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class GetVerifiedAttributeMapping : IGetVerifiedAttributeMapping
{
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;

    public GetVerifiedAttributeMapping(IGetFullAppConfigQuery getFullAppConfigQuery)
    {
        _getFullAppConfigQuery = getFullAppConfigQuery;
    }
    public async Task<dynamic> GetVerifiedAsync(string appId, MappingType type)
    {
        var appConfig = await _getFullAppConfigQuery.GetAsync(appId);

        if (appConfig == null)
            throw new NotFoundException("Application not found");

        if (type == MappingType.Group)
        {
            var groupAttributes = appConfig.GroupAttributeSchemas.ToList();

            return JSONParserUtilV2<Resource>.Parse(groupAttributes, GetDemoUserData());
        }
        else if (type == MappingType.User)
        {
            var userAttributes = appConfig.UserAttributeSchemas.ToList();

            return JSONParserUtilV2<Resource>.Parse(userAttributes, GetDemoUserData());
        }


        return null;
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
                new Role
                {
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
}
