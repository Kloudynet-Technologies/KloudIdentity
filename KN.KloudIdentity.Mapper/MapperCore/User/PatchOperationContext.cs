using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Per-request record of the SCIM attributes targeted by the current PATCH. UpdateUserV4 applies the PATCH to a
/// fresh <see cref="Core2EnterpriseUser"/>, so an integration cannot otherwise tell "set to false / empty" from
/// "not in the PATCH". Populated by UpdateUserV4; integrations that do not use it are unaffected.
/// </summary>
public interface IPatchOperationContext
{
    /// <summary>
    /// True once the operations of the current PATCH request have been captured.
    /// </summary>
    bool IsCaptured { get; }

    /// <summary>
    /// Records the operations of the PATCH request. Operations without a path are ignored, as
    /// <c>Core2EnterpriseUser.Apply</c> ignores them too.
    /// </summary>
    void Capture(IEnumerable<PatchOperation2Base>? operations);

    /// <summary>
    /// Forgets the captured operations (IsCaptured becomes false), so they do not leak into a later operation
    /// in the same scope.
    /// </summary>
    void Reset();

    /// <summary>
    /// True when the SCIM attribute read by a mapping SourceValue (e.g. "ElectronicMailAddresses[0]:Value",
    /// "EnterpriseExtension:EmployeeNumber") is targeted by any operation (add, replace or remove).
    /// </summary>
    bool IsPatched(string sourceValue);

    /// <summary>
    /// True when the attribute is targeted by an add or replace operation (a value was set, not removed).
    /// </summary>
    bool IsSet(string sourceValue);
}

public sealed class PatchOperationContext : IPatchOperationContext
{
    private static readonly ConcurrentDictionary<string, ScimAttributePath?> SourceValuePaths =
        new(StringComparer.Ordinal);

    private readonly List<(ScimAttributePath Path, OperationName Operation)> _operations = [];

    public bool IsCaptured { get; private set; }

    public void Capture(IEnumerable<PatchOperation2Base>? operations)
    {
        _operations.Clear();

        foreach (var operation in operations ?? [])
        {
            IPath? path;
            try
            {
                path = operation?.Path;
            }
            catch (ArgumentException)
            {
                // Invalid path expression: skipped here; Core2EnterpriseUser.Apply reports it as before
                continue;
            }

            if (string.IsNullOrWhiteSpace(path?.AttributePath))
                continue;

            _operations.Add((ScimAttributePath.FromPatchPath(path), operation!.Name));
        }

        IsCaptured = true;
    }

    public void Reset()
    {
        _operations.Clear();
        IsCaptured = false;
    }

    public bool IsPatched(string sourceValue) => Matches(sourceValue, includeRemove: true);

    public bool IsSet(string sourceValue) => Matches(sourceValue, includeRemove: false);

    private bool Matches(string sourceValue, bool includeRemove)
    {
        if (string.IsNullOrWhiteSpace(sourceValue))
            return false;

        var mapped = SourceValuePaths.GetOrAdd(sourceValue.Trim(), ScimAttributePath.FromSourceValue);
        if (mapped is null)
            return false;

        return _operations.Any(o =>
            (includeRemove || o.Operation != OperationName.Remove) && o.Path.Covers(mapped.Value));
    }
}

/// <summary>
/// Normalized SCIM attribute path: Root is the attribute name (prefixed with the schema identifier for
/// extension attributes), Sub the optional sub-attribute. Both lower-case.
/// </summary>
internal readonly record struct ScimAttributePath(string Root, string? Sub)
{
    private static readonly string[] ExtensionSchemas =
    [
        SchemaIdentifiers.Core2EnterpriseUser,
        SchemaIdentifiers.Core2KIUser
    ];

    /// <summary>
    /// A patched attribute covers a mapped one when the roots match and the sub-attributes do not differ,
    /// e.g. "emails" covers "emails.value", and "name.givenName" does not cover "name.familyName".
    /// </summary>
    public bool Covers(ScimAttributePath mapped) =>
        Root == mapped.Root && (Sub is null || mapped.Sub is null || Sub == mapped.Sub);

    /// <summary>
    /// From a parsed PATCH path, e.g. emails[type eq "work"].value → (emails, value).
    /// </summary>
    public static ScimAttributePath FromPatchPath(IPath path)
    {
        var root = path.AttributePath;
        if (ExtensionSchemas.Any(s => string.Equals(s, path.SchemaIdentifier, StringComparison.OrdinalIgnoreCase)))
            root = $"{path.SchemaIdentifier}:{root}";

        return new ScimAttributePath(root.ToLowerInvariant(), path.ValuePath?.AttributePath?.ToLowerInvariant());
    }

    /// <summary>
    /// From a mapping SourceValue (Core2EnterpriseUser property path, as read by JSONParserUtilV2), using the
    /// SCIM names of the properties ([DataMember]). Null when the path does not resolve.
    /// </summary>
    public static ScimAttributePath? FromSourceValue(string sourceValue)
    {
        var segments = sourceValue.Split(':').Select(StripIndex).ToArray();

        var first = ResolveMember(typeof(Core2EnterpriseUser), segments[0]);
        if (first is null)
            return null;

        // Extension object (e.g. EnterpriseExtension): its SCIM name is the schema identifier
        if (first.Value.Name.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length < 2)
                return null;

            var attribute = ResolveMember(first.Value.Type, segments[1]);
            if (attribute is null)
                return null;

            var extensionSub = segments.Length > 2 ? ResolveMember(ElementType(attribute.Value.Type), segments[2]) : null;
            return new ScimAttributePath($"{first.Value.Name}:{attribute.Value.Name}".ToLowerInvariant(),
                extensionSub?.Name.ToLowerInvariant());
        }

        var sub = segments.Length > 1 ? ResolveMember(ElementType(first.Value.Type), segments[1]) : null;
        return new ScimAttributePath(first.Value.Name.ToLowerInvariant(), sub?.Name.ToLowerInvariant());
    }

    private static (string Name, Type Type)? ResolveMember(Type type, string propertyName)
    {
        var property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name == propertyName);
        if (property is null)
            return null;

        var name = property.GetCustomAttribute<DataMemberAttribute>(inherit: true)?.Name ?? property.Name;
        return (name, property.PropertyType);
    }

    private static Type ElementType(Type type)
    {
        if (type == typeof(string))
            return type;

        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable is not null && typeof(IEnumerable).IsAssignableFrom(type)
            ? enumerable.GetGenericArguments()[0]
            : type;
    }

    private static string StripIndex(string segment)
    {
        var index = segment.IndexOf('[');
        return (index >= 0 ? segment[..index] : segment).Trim();
    }
}
