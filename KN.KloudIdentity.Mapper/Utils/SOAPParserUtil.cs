using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.Utils
{
    /// <summary>
    /// Utility to dynamically build SOAP XML payloads from a template and mapping config, similar to JSONParserUtilV2.
    /// </summary>
    public class SOAPParserUtil<T> where T : Resource
    {
        /// <summary>
        /// Builds a SOAP XML payload by replacing placeholders in the template with values from the resource, using the mapping config.
        /// </summary>
        /// <param name="xmlTemplate">The SOAP XML template string with placeholders (e.g., {{UserName}}).</param>
        /// <param name="mappingConfig">A list of mapping configs, each mapping a placeholder to a resource property.</param>
        /// <param name="resource">The SCIM resource object.</param>
        /// <returns>The SOAP XML payload as a string.</returns>
        public static string BuildPayload(string xmlTemplate, IList<AttributeSchema> mappingConfig, T resource)
        {
            string result = xmlTemplate;
            foreach (var mapping in mappingConfig)
            {
                object? value = GetValue(resource, mapping);
                string stringValue = value?.ToString() ?? string.Empty;

                // Replace all occurrences of the placeholder (e.g., {{UserName}})
                result = result.Replace($"{{{{{mapping.DestinationField}}}}}", stringValue);
            }
            return result;
        }

        /// <summary>
        /// Gets the value from the resource object based on the mapping config.
        /// Supports direct mapping, constants, and nested properties.
        /// </summary>
        private static object? GetValue(T resource, AttributeSchema mapping)
        {
            if (mapping.MappingType == MappingTypes.Constant)
                return mapping.SourceValue;
            // Support nested property access (e.g., emails[0].value)
            return ReadProperty(resource, mapping.SourceValue);
        }

        /// <summary>
        /// Reads a (possibly nested) property from the resource object using reflection.
        /// Supports array indexing (e.g., emails[0].value).
        /// </summary>
        private static object? ReadProperty(object obj, string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath) || obj == null)
                return null;

            var parts = propertyPath.Split('.');
            object? current = obj;
            foreach (var part in parts)
            {
                if (current == null) return null;

                var match = Regex.Match(part, @"^(\w+)(\[(\d+)\])?$", RegexOptions.Compiled);
                if (!match.Success) return null;

                string propName = match.Groups[1].Value;
                int? index = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : (int?)null;
                var prop = current.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) return null;

                current = prop.GetValue(current);
                if (index.HasValue && current is System.Collections.IEnumerable enumerable)
                {
                    int i = 0;
                    foreach (var item in enumerable)
                    {
                        if (i == index.Value)
                        {
                            current = item;
                            break;
                        }
                        i++;
                    }
                }
            }

            return current;
        }
    }
}
