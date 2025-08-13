using Hangfire.Common;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public class InboundMappingConfigTests
{
    private InboundMappingConfig _mappingConfig;
    private readonly IInboundMapper _inboundMapper;
    private JObject _mappedPayload;
    private JObject _usersPayload;

    public InboundMappingConfigTests()
    {
        _mappingConfig = new InboundMappingConfig
        (
            "users",
            new List<InboundAttributeMapping>
            {
                new InboundAttributeMapping("1", MappingTypes.Direct, AttributeDataTypes.String, "userId", "externalId", true, "1"),
                new InboundAttributeMapping("2", MappingTypes.Constant, AttributeDataTypes.String, "en-US", "preferredLanguage", true, "en-US"),
                new InboundAttributeMapping("3", MappingTypes.Direct, AttributeDataTypes.String, "userId", "userId", true, "mail@mail.com"),
                new InboundAttributeMapping("4", MappingTypes.Direct, AttributeDataTypes.String, "firstName", "firstName", true, "John Doe"),
                new InboundAttributeMapping("5", MappingTypes.Direct, AttributeDataTypes.String, "lastName", "lastName", true, "John Doe"),
                new InboundAttributeMapping("6", MappingTypes.Direct, AttributeDataTypes.String, "email", "userPrincipalName", true, "true"),
                new InboundAttributeMapping("7", MappingTypes.Direct, AttributeDataTypes.String, "firstName", "mailNickName", true, "D100"),
                new InboundAttributeMapping("8", MappingTypes.Direct, AttributeDataTypes.Boolean, "active", "isActive", true, "true"),
                new InboundAttributeMapping("9", MappingTypes.Direct, AttributeDataTypes.String, "departmentId", "departmentId", false, ""),
                new InboundAttributeMapping("10", MappingTypes.Direct, AttributeDataTypes.String, "email", "email", false, ""),
                new InboundAttributeMapping("11", MappingTypes.Direct, AttributeDataTypes.DateTime, "startingDate", "startingDate", false, ""),
                new InboundAttributeMapping("12", MappingTypes.Direct, AttributeDataTypes.Number, "sequenceNumber", "empSequenceNumber", false, "")
            }
        );

        var mappedJson = @"
        {
            ""schemas"": [""urn:ietf:params:scim:api:messages:2.0:BulkRequest""],
            ""Operations"": [
                {
                    ""method"": ""POST"",
                    ""bulkId"": ""00aa00aa-bb11-cc22-dd33-44ee44ee44ee"",
                    ""path"": ""/Users"",
                    ""data"": {
                        ""schemas"": [
                            ""urn:ietf:params:scim:schemas:core:2.0:User"",
                            ""urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"",
                            ""urn:ietf:params:scim:schemas:extension:ki:1.0:User""
                        ],
                        ""externalId"": ""U001"",
                        ""preferredLanguage"": ""en-US"",
                        ""urn:ietf:params:scim:schemas:extension:ki:1.0:User"": {
                            ""userId"": ""U001"",
                            ""firstName"": ""John"",
                            ""lastName"": ""Doe"",
                            ""email"": ""john.doe1@mail.com"",
                            ""departmentId"": ""D100"",
                            ""isActive"": true,
                            ""userPrincipalName"": ""john.doe1@mail.com"",
                            ""startingDate"": ""1/1/2021 12:00:00 AM"",
                            ""empSequenceNumber"": 1
                        }
                    }
                },
                {
                    ""method"": ""POST"",
                    ""bulkId"": ""00aa00aa-bb11-cc22-dd33-44ee44ee44ee"",
                    ""path"": ""/Users"",
                    ""data"": {
                        ""schemas"": [
                            ""urn:ietf:params:scim:schemas:core:2.0:User"",
                            ""urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"",
                            ""urn:ietf:params:scim:schemas:extension:ki:1.0:User""
                        ],
                        ""externalId"": ""U002"",
                        ""preferredLanguage"": ""en-US"",
                        ""urn:ietf:params:scim:schemas:extension:ki:1.0:User"": {
                            ""userId"": ""U002"",
                            ""firstName"": ""Jane"",
                            ""lastName"": ""Smith"",
                            ""email"": ""jane.smith1@mail.com"",
                            ""departmentId"": ""D101"",
                            ""isActive"": true,
                            ""userPrincipalName"": ""jane.smith1@mail.com"",
                            ""startingDate"": ""1/1/2023 12:00:00 AM"",
                            ""empSequenceNumber"": 2
                        }
                    }
                }
            ],
            ""failOnErrors"": null
        }";

        var inputJsone = @"
        {
            ""users"": [
                {
                    ""userId"": ""U001"",
                    ""firstName"": ""John"",
                    ""lastName"": ""Doe"",
                    ""email"": ""john.doe@example.com"",
                    ""departmentId"": ""D100"",
                    ""active"": true,
                    ""startingDate"": ""2021-01-01"",
                    ""sequenceNumber"": 1
                },
                {
                    ""userId"": ""U002"",
                    ""firstName"": ""Jane"",
                    ""lastName"": ""Smith"",
                    ""email"": ""jane.smith@example.com"",
                    ""departmentId"": ""D101"",
                    ""active"": true,
                    ""startingDate"": ""2023-01-01"",
                    ""sequenceNumber"": 2
                }
            ]
        }";

        _mappedPayload = JObject.Parse(mappedJson);
        _usersPayload = JObject.Parse(inputJsone);

        _inboundMapper = new InboundMapper();
    }

    [Fact]
    public async void ValidateMappingConfigs_MissingMandatoryFields_False()
    {
        // Arrange
        // Remove the first mapping config which is mandatory
        List<InboundAttributeMapping> inboundAttributeMappings = _mappingConfig.InboundAttributeMappings.Where(x => x.Id != "1").ToList();
        InboundMappingConfig mappingConfig = new InboundMappingConfig(_mappingConfig.InboundAttMappingUsersPath, inboundAttributeMappings);

        // Act
        var result = await _inboundMapper.ValidateMappingConfigAsync(mappingConfig);

        // Assert
        Assert.False(result.Item1);
        Assert.Contains("The following mandatory fields are missing: externalId", result.Item2);
    }

    [Fact]
    public async void ValidateMappingConfigs_MissingValuePath_False()
    {
        // Arrange
        // Change the data type of the first mapping config to boolean
        List<InboundAttributeMapping> inboundAttributeMappings = _mappingConfig.InboundAttributeMappings.Select(x => x.Id == "1" ? new InboundAttributeMapping(x.Id, x.MappingType, x.DataType, "", x.EntraIdAttribute, x.IsRequired, x.DefaultValue) : x).ToList();
        InboundMappingConfig mappingConfig = new InboundMappingConfig(_mappingConfig.InboundAttMappingUsersPath, inboundAttributeMappings);

        // Act
        var result = await _inboundMapper.ValidateMappingConfigAsync(mappingConfig);

        // Assert
        Assert.False(result.Item1);
        Assert.Contains("The ValuePath is missing for the mapping configuration field: externalId", result.Item2);
    }

    [Fact]
    public async void ValidateMappingConfigs_MissingDefaultValue_False()
    {
        // Arrange
        // Change the data type of the first mapping config to boolean
        List<InboundAttributeMapping> inboundAttributeMappings = _mappingConfig.InboundAttributeMappings.Select(x => x.Id == "1" ? new InboundAttributeMapping(x.Id, x.MappingType, x.DataType, x.ValuePath, x.EntraIdAttribute, x.IsRequired, "") : x).ToList();
        InboundMappingConfig mappingConfig = new InboundMappingConfig(_mappingConfig.InboundAttMappingUsersPath, inboundAttributeMappings);

        // Act
        var result = await _inboundMapper.ValidateMappingConfigAsync(mappingConfig);

        // Assert
        Assert.False(result.Item1);
        Assert.Contains("The DefaultValue is missing for the mapping configuration field: externalId", result.Item2);
    }

    [Fact]
    public async void ValidateMappingConfigs_InvalidDataType_False()
    {
        // Arrange
        // Change the data type of the first mapping config to boolean
        List<InboundAttributeMapping> inboundAttributeMappings = _mappingConfig.InboundAttributeMappings.Select(x => x.Id == "1" ? new InboundAttributeMapping(x.Id, x.MappingType, AttributeDataTypes.Object, x.ValuePath, x.EntraIdAttribute, x.IsRequired, x.DefaultValue) : x).ToList();
        InboundMappingConfig mappingConfig = new InboundMappingConfig(_mappingConfig.InboundAttMappingUsersPath, inboundAttributeMappings);

        // Act
        var result = await _inboundMapper.ValidateMappingConfigAsync(mappingConfig);

        // Assert
        Assert.False(result.Item1);
        Assert.Contains($"The data type for the mapping configuration field externalId is invalid: {AttributeDataTypes.Object}", result.Item2);
    }

    [Fact]
    public async void ValidateMappingConfigs_InvalidMappingType_False()
    {
        // Arrange
        // Change the mapping type of the first mapping config to constant
        List<InboundAttributeMapping> inboundAttributeMappings = _mappingConfig.InboundAttributeMappings.Select(x => x.Id == "1" ? new InboundAttributeMapping(x.Id, MappingTypes.Custom, x.DataType, x.ValuePath, x.EntraIdAttribute, x.IsRequired, x.DefaultValue) : x).ToList();
        InboundMappingConfig mappingConfig = new InboundMappingConfig(_mappingConfig.InboundAttMappingUsersPath, inboundAttributeMappings);

        // Act
        var result = await _inboundMapper.ValidateMappingConfigAsync(mappingConfig);

        // Assert
        Assert.False(result.Item1);
        Assert.Contains($"The mapping type for the mapping configuration field externalId is invalid: {MappingTypes.Custom}", result.Item2);
    }

    [Fact]
    public async void ValidateMappingConfigs_ValidMappingConfigs_ReturnsTrue()
    {
        // Arrange
        // Act
        var result = await _inboundMapper.ValidateMappingConfigAsync(_mappingConfig);

        // Assert
        Assert.True(result.Item1);
    }

    [Fact]
    public async void ValidateMappedPayload_MissingMandatoryFields_False()
    {
        // Arrange
        // Remove the externalId from the first user
        var payload = _mappedPayload.DeepClone().ToObject<JObject>();
        payload!["Operations"]![0]!["data"]!.SelectToken("externalId")!.Parent!.Remove();
        payload!["Operations"]![1]!["bulkId"]!.Parent!.Remove();

        // Act
        var result = await _inboundMapper.ValidateMappedPayloadAsync(payload);

        // Assert
        Assert.False(result.Item1);
        Assert.True(result.Item2.Length == 2);
        Assert.Contains("The following mandatory field is missing: externalId", result.Item2);
    }

    [Fact]
    public async void ValidateMappedPayload_ValidMappedPayload_ReturnsTrue()
    {
        // Arrange
        // Act
        var result = await _inboundMapper.ValidateMappedPayloadAsync(_mappedPayload);

        // Assert
        Assert.True(result.Item1);
    }

    [Fact]
    public async void MapAsync_MissingRequiredField_NoDefaultValue_Throws()
    {
        // Arrange
        // Remove the externalId from the first user
        _usersPayload!["users"]![0]!["userId"]!.Parent!.Remove();
        _mappingConfig.InboundAttributeMappings[0] = new InboundAttributeMapping("1", MappingTypes.Direct, AttributeDataTypes.String, "userId", "externalId", true, "");

        // Act
        // Assert
        await Assert.ThrowsAsync<ApplicationException>(() => _inboundMapper.MapAsync(_mappingConfig, _usersPayload, "correlationId"));
    }

    [Fact]
    public async void MapAsync_MissingRequiredField_NoDefaultValue2_Throws()
    {
        // Arrange
        // Remove the externalId from the first user
        _usersPayload!["users"]![0]!["userId"] = null;
        _mappingConfig.InboundAttributeMappings[0] = new InboundAttributeMapping("1", MappingTypes.Direct, AttributeDataTypes.String, "userId", "externalId", true, "");

        // Act
        // Assert
        await Assert.ThrowsAsync<ApplicationException>(() => _inboundMapper.MapAsync(_mappingConfig, _usersPayload, "correlationId"));
    }

    [Fact]
    public async void MapAsync_MissingRequiredField_WithDefaultValue_Returns()
    {
        // Arrange
        // Remove the externalId from the first user
        _usersPayload!["users"]![0]!["userId"]!.Parent!.Remove();
        _mappingConfig.InboundAttributeMappings[0] = new InboundAttributeMapping("1", MappingTypes.Direct, AttributeDataTypes.String, "userId", "externalId", true, "1");

        // Act
        var result = await _inboundMapper.MapAsync(_mappingConfig, _usersPayload, "correlationId");

        // Assert
        Assert.NotNull(result);

        Assert.Equal("1", result!["Operations"]![0]!["data"]!["externalId"]!.ToString());
    }

    [Fact]
    public async void MapAsync_DirectMapping_Returns()
    {
        // Arrange
        // Act
        var correlationId = Guid.NewGuid().ToString();
        var result = await _inboundMapper.MapAsync(_mappingConfig, _usersPayload, correlationId);

        // Assert
        Assert.NotNull(result);

        Assert.Equal(correlationId, result!["Operations"]![0]!["bulkId"]!.ToString());
        Assert.Equal(correlationId, result!["Operations"]![1]!["bulkId"]!.ToString());

        Assert.Equal("U001", result!["Operations"]![0]!["data"]!["externalId"]!.ToString());
        Assert.Equal("en-US", result!["Operations"]![0]!["data"]!["preferredLanguage"]!.ToString());
        Assert.Equal("U001", result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["userId"]!.ToString());
        Assert.Equal("John", result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["firstName"]!.ToString());
        Assert.Equal("Doe", result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["lastName"]!.ToString());
        Assert.Equal("john.doe@example.com", result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["userPrincipalName"]!.ToString());
        Assert.Equal("John", result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["mailNickName"]!.ToObject<string>());
        Assert.Equal(true, result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["isActive"]!.ToObject<bool>());
        Assert.Equal("D100", result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["departmentId"]!.ToString());
        Assert.Equal("john.doe@example.com", result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["email"]!.ToString());
        Assert.Equal(1, result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["empSequenceNumber"]!.ToObject<int>());
        Assert.Equal(
            DateTime.Parse("1/1/2021 12:00:00 AM"),
            DateTime.Parse(result!["Operations"]![0]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["startingDate"]!.ToString())
        );
        Assert.Equal("U002", result!["Operations"]![1]!["data"]!["externalId"]!.ToString());
        Assert.Equal("en-US", result!["Operations"]![0]!["data"]!["preferredLanguage"]!.ToString());
        Assert.Equal("U002", result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["userId"]!.ToString());
        Assert.Equal("Jane", result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["firstName"]!.ToString());
        Assert.Equal("Smith", result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["lastName"]!.ToString());
        Assert.Equal("jane.smith@example.com", result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["userPrincipalName"]!.ToString());
        Assert.Equal("Jane", result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["mailNickName"]!.ToObject<string>());
        Assert.Equal(true, result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["isActive"]!.ToObject<bool>());
        Assert.Equal("D101", result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["departmentId"]!.ToString());
        Assert.Equal("jane.smith@example.com", result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["email"]!.ToString());
        Assert.Equal(2, result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["empSequenceNumber"]!.ToObject<int>());
        Assert.Equal(
            DateTime.Parse("1/1/2023 12:00:00 AM"),
            DateTime.Parse(result!["Operations"]![1]!["data"]!["urn:ietf:params:scim:schemas:extension:ki:1.0:User"]!["startingDate"]!.ToString())
        );
    }
}
