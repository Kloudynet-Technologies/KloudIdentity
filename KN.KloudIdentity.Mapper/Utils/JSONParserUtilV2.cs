using System.Collections;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper;

public class JSONParserUtilV2<T> where T : Resource
{
    private static bool _isSamplePayload = false;

    /// <summary>
    /// This method parses the resource object values into a JSON object.
    /// </summary>
    /// <param name="schemaAttributes"></param>
    /// <param name="resource"></param>
    /// <returns></returns>
    public static JObject Parse(IList<AttributeSchema> schemaAttributes, T resource, bool isSamplePayload = false)
    {
        JObject jObject = new JObject();
        string urnPrefix = "urn:kn:ki:schema:";
        _isSamplePayload = isSamplePayload;

        foreach (AttributeSchema schemaAttribute in schemaAttributes)
        {
            string attrUrn = schemaAttribute.DestinationField.Remove(0, urnPrefix.Length);
            var attrArray = attrUrn.Split(':');

            if (attrArray.Length > 1)
            {
                // Nested property
                var nestedObject = jObject;
                for (int i = 0; i < attrArray.Length - 1; i++)
                {
                    var nestedPropertyName = attrArray[i];
                    if (nestedObject[nestedPropertyName] == null)
                    {
                        nestedObject[nestedPropertyName] = new JObject();
                    }
                    nestedObject = (JObject)nestedObject[nestedPropertyName];
                }

                if (schemaAttribute.MappingType == MappingTypes.Direct)
                {
                    nestedObject[attrArray[attrArray.Length - 1]] = GetValue(resource, schemaAttribute);
                }
                else if (schemaAttribute.MappingType == MappingTypes.Constant)
                {
                    nestedObject[attrArray[attrArray.Length - 1]] = schemaAttribute.SourceValue;
                }
            }
            else
            {
                // Top-level property
                if (schemaAttribute.MappingType == MappingTypes.Direct)
                {
                    jObject[attrArray[0]] = GetValue(resource, schemaAttribute);
                }
                else if (schemaAttribute.MappingType == MappingTypes.Constant)
                {
                    jObject[attrArray[0]] = schemaAttribute.SourceValue;
                }
            }
        }

        return jObject;
    }

    /// <summary>
    /// Gets the value of the specified schema attribute from the given resource object.
    /// </summary>
    /// <typeparam name="T">The type of the resource object.</typeparam>
    /// <param name="resource">The resource object to get the value from.</param>
    /// <param name="schemaAttribute">The schema attribute to get the value of.</param>
    /// <returns>The value of the specified schema attribute from the given resource object.</returns>
    public static dynamic? GetValue(T resource, AttributeSchema schemaAttribute)
    {
        return schemaAttribute.DestinationType switch
        {
            JsonDataTypes.String => _isSamplePayload ? GetSampleValue<string>(schemaAttribute) :
                                    GetValue<string>(resource, schemaAttribute),
            JsonDataTypes.Boolean => _isSamplePayload ? GetSampleValue<bool>(schemaAttribute) :
                                    GetValue<bool>(resource, schemaAttribute),
            JsonDataTypes.Number => _isSamplePayload ? GetSampleValue<long>(schemaAttribute) :
                                    GetValue<long>(resource, schemaAttribute),
            JsonDataTypes.Object => MakeJsonObject(resource, schemaAttribute),
            JsonDataTypes.Array => MakeJsonArray(resource, schemaAttribute),
            _ => default,
        };
    }

    public static T2 GetSampleValue<T2>(AttributeSchema schemaAttribute)
    {
        if (schemaAttribute.IsRequired)
        {
            if (schemaAttribute.DefaultValue != null)
            {
                return (T2)Convert.ChangeType(schemaAttribute.DefaultValue, typeof(T2));
            }
            else
            {
                return default;
            }
        }
        else
        {
            switch (schemaAttribute.DestinationType)
            {
                case JsonDataTypes.String:
                    return (T2)Convert.ChangeType("string", typeof(T2));
                case JsonDataTypes.Boolean:
                    return (T2)Convert.ChangeType(false, typeof(T2));
                case JsonDataTypes.Number:
                    return (T2)Convert.ChangeType(0, typeof(T2));
                default:
                    return default;
            }
        }
    }

    public static T2? GetValue<T2>(T resource, AttributeSchema schemaAttribute)
    {
        var value = ReadProperty(resource, schemaAttribute.SourceValue);
        if (value != null && !string.IsNullOrEmpty(value.ToString()))
        {
            return (T2)Convert.ChangeType(value, typeof(T2));
        }
        else if (schemaAttribute.IsRequired)
        {
            if (schemaAttribute.DefaultValue != null)
            {
                return (T2)Convert.ChangeType(schemaAttribute.DefaultValue, typeof(T2));
            }
            else
            {
                return default;
            }
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// Reads the value of a property with the given name from the specified resource object.
    /// </summary>
    /// <typeparam name="T">The type of the resource object.</typeparam>
    /// <param name="resource">The resource object to read the property from.</param>
    /// <param name="propertyName">The name of the property to read.</param>
    /// <returns>The value of the property, or null if the property is not found.</returns>
    public static object? ReadProperty(dynamic resource, string propertyName)
    {
        var subProperties = propertyName.Split(':');
        if (subProperties.Length > 1)
        {
            return ReadNestedProperty(resource, subProperties);
        }

        var scimType = resource.GetType();
        var property = scimType.GetProperty(propertyName);
        if (property != null)
        {
            return property.GetValue(resource, null);
        }
        else
        {
            throw new ArgumentException($"Property {propertyName} not found on type {typeof(T).Name}");
        }
    }

    /// <summary>
    /// Reads the value of a nested property with the given name from the specified resource object.
    /// </summary>
    /// <typeparam name="T">The type of the resource object.</typeparam>
    /// <param name="resource">The resource object to read the property from.</param>
    /// <param name="subProperties">The array of sub properties to read.</param>
    /// <returns>The value of the property, or null if the property is not found.</returns>
    public static object? ReadNestedProperty(T resource, string[] subProperties)
    {
        dynamic value = resource;

        foreach (var prop in subProperties)
        {
            if (value == null)
            {
                return value;
            }

            int index = 0;
            string propName = prop;

            var indArr = prop.Split('[');
            if (indArr.Length > 1)
            {
                index = int.Parse(indArr[1].Replace("]", ""));
                propName = indArr[0];
            }

            var propertyInfo = value.GetType().GetProperty(propName);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property {propName} not found on type {value.GetType().Name}");
            }

            Type propertyType = propertyInfo.PropertyType;

            if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType.IsGenericType)
            {
                var collection = propertyInfo.GetValue(value, null) as IEnumerable<object>;
                if (collection != null && index < collection.Count())
                {
                    value = collection.ElementAt(index);
                }
                else
                {
                    value = null;
                }
            }
            else
            {
                value = propertyInfo.GetValue(value);
            }
        }

        return value;
    }

    /// <summary>
    /// Makes a JSON array based on the provided schema attribute.
    /// </summary>
    /// <param name="data">The array of dynamic objects to read values from.</param>
    /// <param name="schemaAttribute">The schema attribute specifying how to read values from the array.</param>
    /// <returns>A JArray containing the extracted values based on the schema attribute.</returns>
    public static dynamic MakeJsonArray(T resource, AttributeSchema schemaAttribute)
    {
        var newArray = new JArray();

        if (schemaAttribute.ArrayDataType == JsonDataTypes.Object)
        {
            var objVal = MakeJsonObject(resource, schemaAttribute);
            newArray.Add(objVal);

            return newArray;
        }

        var data = ReadProperty(resource, schemaAttribute.SourceValue);

        if (data is not IEnumerable<object> && schemaAttribute.ArrayDataType != JsonDataTypes.Object)
        {
            if (schemaAttribute.IsRequired && string.IsNullOrEmpty(data?.ToString()))
                return new JArray(ParseValue(schemaAttribute.DefaultValue, schemaAttribute.ArrayDataType));

            if (!string.IsNullOrEmpty(data?.ToString()))
                return new JArray(ParseValue(data?.ToString(), schemaAttribute.ArrayDataType));

            return new JArray();
        }

        // Check if the array element type is a simple data type (String, Integer, Boolean)
        if (IsSimpleDataType(schemaAttribute.ArrayDataType))
        {
            foreach (var obj in data as IEnumerable)
            {
                var attrArray = schemaAttribute.ArrayElementFieldName!.Split(':');
                var propertyValue = GetPropertyValue(obj, attrArray[attrArray.Length - 1]);
                var propertyValueString = propertyValue?.ToString();

                if (string.IsNullOrEmpty(propertyValueString) && schemaAttribute.IsRequired)
                {
                    newArray.Add(ParseValue(schemaAttribute.DefaultValue, schemaAttribute.ArrayDataType));
                    continue;
                }
                if (!string.IsNullOrEmpty(propertyValueString?.ToString()))
                {
                    var parsedValue = ParseValue(propertyValueString, schemaAttribute.ArrayDataType);
                    newArray.Add(parsedValue);
                }
            }
        }
        //else
        //{
        //    var objVal = MakeJsonObject(resource, schemaAttribute);
        //    newArray.Add(objVal);
        //}

        return newArray;
    }

    private static bool IsSimpleDataType(JsonDataTypes dataType)
    {
        return dataType == JsonDataTypes.String ||
               dataType == JsonDataTypes.Number ||
               dataType == JsonDataTypes.Boolean;
    }

    private static dynamic ParseValue(string? value, JsonDataTypes dataType)
    {
        return dataType switch
        {
            JsonDataTypes.String => value ?? "",
            JsonDataTypes.Number => int.TryParse(value, out var intValue) ? intValue : default(int?),
            JsonDataTypes.Boolean => bool.TryParse(value, out var boolValue) ? boolValue : default(bool?),
            _ => throw new NotSupportedException($"Unsupported array data type: {dataType}")
        };
    }

    /// <summary>
    /// Gets the value of a specified property from a dynamic object.
    /// </summary>
    /// <param name="obj">The dynamic object from which to retrieve the property value.</param>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>The value of the specified property, or null if the property is not found.</returns>
    private static object GetPropertyValue(dynamic obj, string propertyName)
    {
        // Get the property information using reflection
        var property = obj.GetType().GetProperty(propertyName);

        // Return the property value if the property is found, otherwise return null
        return property?.GetValue(obj);
    }

    /// <summary>
    /// Makes a JSON object based on the provided schema attribute.
    /// </summary>
    /// <param name="resource">The resource object to read the property from.</param>
    /// <param name="schemaAttribute">The schema attribute specifying how to read values from the resource.</param>
    /// <returns>
    /// Returns a JObject containing the extracted values based on the schema attribute.
    /// </returns>
    private static object? MakeJsonObject(T resource, AttributeSchema schemaAttribute)
    {
        dynamic childObject = new System.Dynamic.ExpandoObject();

        foreach (var childSchema in schemaAttribute.ChildSchemas)
        {
            string urnPrefix = "urn:kn:ki:schema:";
            string attrUrn = childSchema.DestinationField.Remove(0, urnPrefix.Length);
            var attrArray = attrUrn.Split(':');

            if (childSchema.DestinationType == JsonDataTypes.Array)
            {
                var childValue = ReadProperty(resource, childSchema.SourceValue);
                ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], MakeJsonArray(resource, childSchema));
                continue;
            }
            else
            {
                if (childSchema.DestinationType == JsonDataTypes.Object)
                {
                    var childValue = ReadProperty(resource, childSchema.SourceValue);
                    ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], MakeJsonObject(resource, childSchema));
                    continue;
                }
                else
                {
                    // var childValue = ReadProperty(resource, childSchema.SourceValue);
                    dynamic childValue;

                    if (childSchema.MappingType == MappingTypes.Constant)
                    {
                        childValue = childSchema.SourceValue;
                    }
                    else
                    {
                        switch (childSchema.DestinationType)
                        {
                            case JsonDataTypes.String:
                                childValue = GetValue<string>(resource, childSchema);
                                break;
                            case JsonDataTypes.Boolean:
                                childValue = GetValue<bool>(resource, childSchema);
                                break;
                            case JsonDataTypes.Number:
                                childValue = GetValue<long>(resource, childSchema);
                                break;
                            default:
                                childValue = default;
                                break;
                        }

                    }

                    ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], childValue);
                }
            }
        }

        return JObject.FromObject(childObject);
    }
}