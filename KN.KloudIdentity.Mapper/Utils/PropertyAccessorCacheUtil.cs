using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace KN.KloudIdentity.Mapper.Utils;

public static class PropertyAccessorCacheUtil
{
    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        public Type Type { get; }
        public string PropertyName { get; }

        public CacheKey(Type type, string propertyName)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }

        public bool Equals(CacheKey other) =>
            Type == other.Type &&
            StringComparer.OrdinalIgnoreCase.Equals(PropertyName, other.PropertyName);

        public override bool Equals(object? obj) =>
            obj is CacheKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Type, StringComparer.OrdinalIgnoreCase.GetHashCode(PropertyName));
    }

    private static readonly ConcurrentDictionary<CacheKey, Func<object, object?>> _cache = new();

    public static string? GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

        var type = obj.GetType();
        var key = new CacheKey(type, propertyName);

        if (!_cache.TryGetValue(key, out var accessor))
        {
            var prop = type.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyName}' not found on {type.Name}.", nameof(propertyName));

            var param = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(param, type);
            var propertyAccess = Expression.Property(cast, prop);
            var convertResult = Expression.Convert(propertyAccess, typeof(object));
            accessor = Expression.Lambda<Func<object, object?>>(convertResult, param).Compile();

            _cache[key] = accessor;
        }

        return accessor(obj)?.ToString();
    }
}
