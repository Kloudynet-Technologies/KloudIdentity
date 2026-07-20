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
        private const string UrnPrefix = "urn:kn:ki:schema:";

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

                // Escape XML special characters to prevent invalid XML and injection
                string escapedValue = System.Security.SecurityElement.Escape(stringValue) ?? string.Empty;

                // DestinationField arrives URN-qualified (e.g., urn:kn:ki:schema:userId) while
                // templates use the bare field name as placeholder (e.g., {{userId}})
                string fieldName = mapping.DestinationField.Replace(UrnPrefix, string.Empty);

                // Replace all occurrences of the placeholder (e.g., {{UserName}}) with the escaped value
                result = result.Replace($"{{{{{fieldName}}}}}", escapedValue);
            }
            return result;
        }

        /// <summary>
        /// Gets the value from the resource object based on the mapping config.
        /// Supports direct mapping, constants, and nested properties.
        /// </summary>
        private static object? GetValue(T resource, AttributeSchema mapping)
        {
            // Resolve the raw value from either a constant or the source property path.
            object? value;
            if (mapping.MappingType == MappingTypes.Constant)
            {
                value = mapping.SourceValue;
            }
            else
            {
                // Support nested property access (e.g., emails[0].value)
                value = string.IsNullOrWhiteSpace(mapping.SourceValue)
                    ? null
                    : ReadProperty(resource, mapping.SourceValue);
            }

            // Apply default value if the resolved value is null or empty.
            bool IsNullOrEmpty(object? v) =>
                v == null || (v is string sv && string.IsNullOrEmpty(sv));
            if (IsNullOrEmpty(value) && mapping.DefaultValue != null)
            {
                value = mapping.DefaultValue;
            }

            // Enforce required semantics: if still null/empty, fail fast.
            if (mapping.IsRequired && IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    $"Required mapping '{mapping.DestinationField}' could not be resolved from source '{mapping.SourceValue}'.");
            }

            // Attempt to coerce the value to the desired destination type, if provided.
            // DestinationType is assumed to be compatible with System.Type; if not, this cast will simply yield null.
            switch (mapping.DestinationType)
            {
                case AttributeDataTypes.String:
                    value = value?.ToString();
                    break;
                case AttributeDataTypes.Number:
                    if (value != null && double.TryParse(value.ToString(), out var num))
                        value = num;
                    break;
                case AttributeDataTypes.Boolean:
                    if (value != null && bool.TryParse(value.ToString(), out var b))
                        value = b;
                    break;
                case AttributeDataTypes.DateTime:
                    if (value != null && DateTime.TryParse(value.ToString(), out var dt))
                        value = dt;
                    break;
                default:
                    value = value?.ToString();
                    break;

                    // Add more type coercions as needed
            }

            return value;
        }

        /// <summary>
        /// Reads a (possibly nested) property from the resource object using reflection.
        /// Supports array indexing (e.g., emails[0].value).
        /// </summary>
        private static object? ReadProperty(object obj, string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath) || obj == null)
                return null;

            // Support both legacy dot paths (emails[0].value) and URN-style colon paths (emails[0]:value)
            var parts = propertyPath.Split(':', '.');
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
