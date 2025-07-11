using System.Linq;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;

public class InboundMapper : IInboundMapper
{
    public virtual Task<JObject> GetSCIMPayloadTemplateAsync()
    {
        return Task.FromResult(JObject.Parse(InboundConstants.SCIM_EXTENSION_TEMPLATE));
    }

    // This method is used to map the incoming payload to the SCIM payload
    public virtual Task<JObject> MapAsync(InboundMappingConfig mappingConfig, JObject usersPayload,
        string correlationId)
    {
        var scimTemplate = JObject.Parse(InboundConstants.SCIM_EXTENSION_TEMPLATE);
        var scimPayload = new JObject(scimTemplate);
        var transformedUsers = new JArray();

        var userTemplate = JObject.Parse(InboundConstants.SCIM_USER_TEMPLATE);

        var users = usersPayload[mappingConfig.InboundAttMappingUsersPath];
        if (users == null || !users.HasValues)
        {
            Log.Error(
                "The path {Path} does not exist or contains users in the incoming payload. CorrelationId: {CorrelationId}",
                mappingConfig.InboundAttMappingUsersPath, correlationId);
            throw new ApplicationException(
                $"The path {mappingConfig.InboundAttMappingUsersPath} does not exist or contains users in the incoming payload.");
        }

        foreach (var user in users)
        {
            var scimUser = new JObject(userTemplate!);
            scimUser["bulkId"] = correlationId;

            foreach (var mapping in mappingConfig.InboundAttributeMappings)
            {
                JToken value = JValue.CreateNull();

                if (mapping.MappingType == MappingTypes.Constant)
                {
                    value = mapping.ValuePath;
                }
                else if (mapping.MappingType == MappingTypes.Direct)
                {
                    value = user.SelectToken(mapping.ValuePath) ?? JValue.CreateNull();
                    if (value == null || string.IsNullOrEmpty(value.ToString()))
                    {
                        if (mapping.IsRequired && string.IsNullOrEmpty(mapping.DefaultValue))
                        {
                            Log.Error(
                                "The value for the field {Field} is required but is missing in the incoming payload or default value. CorrelationId: {CorrelationId}",
                                mapping.EntraIdAttribute, correlationId);
                            throw new ApplicationException(
                                $"The value for the field {mapping.EntraIdAttribute} is required, but it is missing in the incoming payload or default value.");
                        }

                        value = mapping.DefaultValue;
                    }
                }

                if (value != null)
                {
                    if (mapping.EntraIdAttribute == "externalId" || mapping.EntraIdAttribute == "preferredLanguage")
                    {
                        scimUser["data"]![mapping.EntraIdAttribute] = value.ToString();
                    }
                    else
                    {
                        switch (mapping.DataType)
                        {
                            case AttributeDataTypes.String:
                                scimUser["data"]![InboundConstants.SCIM_USER_EXTENSION_SCHEMA]![
                                    mapping.EntraIdAttribute] = value.ToString();
                                break;
                            case AttributeDataTypes.Boolean:
                                scimUser["data"]![InboundConstants.SCIM_USER_EXTENSION_SCHEMA]![
                                    mapping.EntraIdAttribute] = bool.Parse(value.ToString());
                                break;
                            case AttributeDataTypes.Number:
                                scimUser["data"]![InboundConstants.SCIM_USER_EXTENSION_SCHEMA]![
                                    mapping.EntraIdAttribute] = int.Parse(value.ToString());
                                break;
                            case AttributeDataTypes.DateTime:
                                scimUser["data"]![InboundConstants.SCIM_USER_EXTENSION_SCHEMA]![
                                    mapping.EntraIdAttribute] = DateTime.Parse(value.ToString());
                                break;
                            default:
                                throw new Exception($"The data type {mapping.DataType} is not supported.");
                        }
                    }
                }
            }

            transformedUsers.Add(scimUser);
        }

        scimPayload["Operations"] = transformedUsers;

        return Task.FromResult(scimPayload);
    }

    public virtual Task<(bool, string[])> ValidateMappedPayloadAsync(JObject payload)
    {
        var errors = new List<string>();

        if (payload == null || !payload.HasValues)
        {
            errors.Add("The payload is null or empty");

            return Task.FromResult((false, errors.ToArray()));
        }

        if (payload["schemas"] == null || !payload["schemas"]!.HasValues ||
            payload["schemas"]![0]!.Value<string>() != InboundConstants.BULKREQUEST_SCHEMA)
        {
            errors.Add("The schemas field is missing or invalid");
        }

        if (payload["Operations"] == null || !payload["Operations"]!.HasValues)
        {
            errors.Add("The Operations field is empty");
        }

        foreach (var user in payload["Operations"]!)
        {
            if (user["bulkId"] == null || string.IsNullOrEmpty(user["bulkId"]!.Value<string>()))
            {
                errors.Add("The following mandatory field is missing: bulkId");
            }

            if (user["data"] == null || !user["data"]!.HasValues)
            {
                errors.Add("The data object is missing or empty");
            }

            if (user["data"]!["externalId"] == null ||
                string.IsNullOrEmpty(user["data"]!["externalId"]!.Value<string>()))
            {
                errors.Add("The following mandatory field is missing: externalId");
            }

            if (user["data"]!["preferredLanguage"] == null ||
                string.IsNullOrEmpty(user["data"]!["preferredLanguage"]!.Value<string>()))
            {
                errors.Add("The following mandatory field is missing: preferredLanguage");
            }
        }

        return Task.FromResult((errors.Count == 0, errors.ToArray()));
    }

    public virtual Task<(bool, string[])> ValidateMappingConfigAsync(InboundMappingConfig mappingConfig)
    {
        var errors = new List<string>();

        if (mappingConfig == null ||
            mappingConfig.InboundAttributeMappings == null ||
            mappingConfig.InboundAttributeMappings.Count == 0)
        {
            errors.Add("Mapping configurations are empty.");

            return Task.FromResult((false, errors.ToArray()));
        }

        // Check for invalid mapping types
        var invalidMappingTypes = mappingConfig.InboundAttributeMappings
            .Where(x => !InboundConstants.VALID_MAPPING_TYPES.Contains(x.MappingType))
            .Select(x => (x.EntraIdAttribute, x.MappingType)).ToList();
        if (invalidMappingTypes.Count > 0)
        {
            invalidMappingTypes.ForEach(x =>
                errors.Add(
                    $"The mapping type for the mapping configuration field {x.EntraIdAttribute} is invalid: {x.MappingType}"));
        }

        // Check for invalid data types
        var invalidDataTypes = mappingConfig.InboundAttributeMappings
            .Where(x => !InboundConstants.VALID_DATA_TYPES.Contains(x.DataType))
            .Select(x => (x.EntraIdAttribute, x.DataType)).ToList();
        if (invalidDataTypes.Count > 0)
        {
            invalidDataTypes.ForEach(x =>
                errors.Add(
                    $"The data type for the mapping configuration field {x.EntraIdAttribute} is invalid: {x.DataType}"));
        }

        // Check for missing mandatory fields
        var missingMandatoryFields = InboundConstants.MANDATORY_ATTRIBUTES
            .Except(mappingConfig.InboundAttributeMappings.Select(x => x.EntraIdAttribute)).ToList();
        if (missingMandatoryFields.Count > 0)
        {
            missingMandatoryFields.ForEach(x => errors.Add($"The following mandatory fields are missing: {x}"));
        }

        // Check for missing value path
        var missingValuePath = mappingConfig.InboundAttributeMappings.Where(x => string.IsNullOrEmpty(x.ValuePath))
            .Select(x => x.EntraIdAttribute).ToList();
        if (missingValuePath.Count > 0)
        {
            missingValuePath.ForEach(x =>
                errors.Add($"The ValuePath is missing for the mapping configuration field: {x}"));
        }

        // Check for missing default value
        var missingDefaultValue = mappingConfig.InboundAttributeMappings
            .Where(x => x.IsRequired && string.IsNullOrEmpty(x.DefaultValue)).Select(x => x.EntraIdAttribute).ToList();
        if (missingDefaultValue.Count > 0)
        {
            missingDefaultValue.ForEach(x =>
                errors.Add($"The DefaultValue is missing for the mapping configuration field: {x}"));
        }

        return Task.FromResult((errors.Count == 0, errors.ToArray()));
    }
}