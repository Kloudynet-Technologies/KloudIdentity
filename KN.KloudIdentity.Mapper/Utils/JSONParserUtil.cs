﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Collections;
using KN.KloudIdentity.Mapper.Config;
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
        public static JObject Parse(IList<SchemaAttribute> schemaAttributes, T resource)
        {
            JObject jObject = new JObject();
            string urnPrefix = "urn:kn:ki:schema:";

            foreach (SchemaAttribute schemaAttribute in schemaAttributes)
            {
                string attrUrn = schemaAttribute.FieldName.Remove(0, urnPrefix.Length);
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
        public static dynamic? GetValue(T resource, SchemaAttribute schemaAttribute)
        {
            var value = ReadProperty(resource, schemaAttribute.MappedAttribute);

            return schemaAttribute.DataType switch
            {
                JSonDataType.String => value?.ToString(),
                JSonDataType.Boolean => Boolean.TryParse(value?.ToString(), out bool boolValue) ? boolValue : default(bool?),
                JSonDataType.Integer => Int32.TryParse(value?.ToString(), out int intValue) ? intValue : default(int?),
                JSonDataType.Object => throw new NotImplementedException("Object type not implemented yet."),
                JSonDataType.Array => throw new NotImplementedException("Array type not implemented yet."),
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
        public static object? ReadProperty(T resource, string propertyName)
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
    }
}