//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Collections;
using System.Dynamic;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.Utils
{
    /// <summary>
    /// This class contains the utility methods for parsing JSON.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class JSONParserUtil<T> where T : Resource
    {
        /// <summary>
        /// This method parses the resource object values into a JSON object.
        /// </summary>
        /// <param name="schemaAttributes"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public static JObject Parse(IList<AttributeSchema> schemaAttributes, T resource)
        {
            JObject jObject = new JObject();
            string urnPrefix = "urn:kn:ki:schema:";

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
                    nestedObject[attrArray[attrArray.Length - 1]] = GetValue(resource, schemaAttribute);
                }
                else
                {
                    // Top-level property
                    jObject[attrArray[0]] = GetValue(resource, schemaAttribute);
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
            var value = ReadProperty(resource, schemaAttribute.SourceValue);

            return schemaAttribute.DestinationType switch
            {
                AttributeDataTypes.String => value?.ToString(),
                AttributeDataTypes.Boolean => Boolean.TryParse(value?.ToString(), out bool boolValue) ? boolValue : default(bool?),
                AttributeDataTypes.Number => Int32.TryParse(value?.ToString(), out int intValue) ? intValue : default(int?),
                AttributeDataTypes.Object => MakeJsonObject(resource, schemaAttribute),
                AttributeDataTypes.Array => MakeJsonArray(resource, value, schemaAttribute),
                _ => default,
            };
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
                    throw new ArgumentException($"Property {prop} is null");
                }

                Type valueType = value.GetType();
                if (typeof(IEnumerable).IsAssignableFrom(valueType) && valueType.IsGenericType)
                {
                    value = value[0];
                }

                var propertyInfo = value.GetType().GetProperty(prop);

                if (propertyInfo == null)
                {
                    throw new ArgumentException($"Property {prop} not found on type {value.GetType().Name}");
                }

                value = propertyInfo.GetValue(value);
            }

            return value;
        }

        /// <summary>
        /// Makes a JSON array based on the provided schema attribute.
        /// </summary>
        /// <param name="data">The array of dynamic objects to read values from.</param>
        /// <param name="schemaAttribute">The schema attribute specifying how to read values from the array.</param>
        /// <returns>A JArray containing the extracted values based on the schema attribute.</returns>
        public static dynamic MakeJsonArray(T resource, dynamic data, AttributeSchema schemaAttribute)
        {
            var newArray = new JArray();

            // Check if the array element type is a simple data type (String, Integer, Boolean)
            if (schemaAttribute.ArrayDataType == AttributeDataTypes.String ||
                schemaAttribute.ArrayDataType == AttributeDataTypes.Number ||
                schemaAttribute.ArrayDataType == AttributeDataTypes.Boolean)
            {
                // Iterate through each object in the array
                foreach (var obj in data)
                {
                    var attrArray = schemaAttribute.SourceValue.Split(':');

                    // Get the value of the specified property from the object
                    var propertyValue = GetPropertyValue(obj, attrArray[attrArray.Length - 1]);

                    // If the property value is not null, convert and add it to the new array
                    if (propertyValue != null)
                    {
                        switch (schemaAttribute.ArrayDataType)
                        {
                            case AttributeDataTypes.String:
                                newArray.Add(propertyValue.ToString());
                                break;

                            case AttributeDataTypes.Number:
                                newArray.Add(Int32.TryParse(propertyValue?.ToString(), out int intValue) ? intValue : default(int?));
                                break;

                            case AttributeDataTypes.Boolean:
                                newArray.Add(Boolean.TryParse(propertyValue?.ToString(), out bool boolValue) ? boolValue : default(bool?));
                                break;
                        }
                    }
                }
            }
            else
            {
                // Iterate through each object in the array
                foreach (var obj in data)
                {
                    // Create a dynamic object based on the child schema
                    dynamic childObject = new System.Dynamic.ExpandoObject();

                    foreach (var childSchema in schemaAttribute.ChildSchemas)
                    {
                        string urnPrefix = "urn:kn:ki:schema:";
                        string attrUrn = childSchema.DestinationField.Remove(0, urnPrefix.Length);
                        var attrArray = attrUrn.Split(':');

                        if (childSchema.DestinationType == AttributeDataTypes.Array)
                        {
                            var childValue = GetPropertyValue(obj, childSchema.SourceValue);
                            ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], MakeJsonArray(resource, childValue, childSchema));
                            continue;
                        }
                        else
                        {
                            var childValue = GetPropertyValue(obj, childSchema.SourceValue);
                            ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], childValue);
                        }

                    }

                    // Convert the dynamic childObject to a JObject
                    var childObjectJson = JObject.FromObject(childObject);

                    newArray.Add(childObjectJson);
                }

            }

            return newArray;
        }

        /// <summary>
        /// Gets the value of a specified property from a dynamic object.
        /// </summary>
        /// <param name="obj">The dynamic object from which to retrieve the property value.</param>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        /// <returns>The value of the specified property, or null if the property is not found.</returns>
        private static object GetPropertyValue(dynamic obj, string propertyName)
        {
            var attrArray = propertyName.Split(':');
            // Get the property information using reflection
            var property = obj.GetType().GetProperty(attrArray[attrArray.Length - 1]);

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

                if (childSchema.DestinationType == AttributeDataTypes.Array)
                {
                    var childValue = ReadProperty(resource, childSchema.SourceValue);
                    ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], MakeJsonArray(resource, childValue, childSchema));
                    continue;
                }
                else
                {
                    if (childSchema.DestinationType == AttributeDataTypes.Object)
                    {
                        var childValue = ReadProperty(resource, childSchema.SourceValue);
                        ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], MakeJsonObject(resource, childSchema));
                        continue;
                    }
                    else
                    {
                        var childValue = ReadProperty(resource, childSchema.SourceValue);
                        ((IDictionary<string, object>)childObject).Add(attrArray[attrArray.Length - 1], childValue);
                    }

                }

            }

            return JObject.FromObject(childObject);
        }
    }
}
