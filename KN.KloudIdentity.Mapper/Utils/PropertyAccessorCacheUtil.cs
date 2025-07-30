using System;
using System.Reflection;
using System.Collections.Concurrent;

namespace KN.KloudIdentity.Mapper.Utils;

public static class PropertyAccessorCacheUtil
{
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object>> _cache = new();

    public static string? GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        var type = obj.GetType();
        var key = (type, propertyName.ToLowerInvariant());

        if (!_cache.TryGetValue(key, out var accessor))
        {
            var prop = type.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyName}' not found on {type.Name}.", nameof(propertyName));

            var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "obj");
            var cast = System.Linq.Expressions.Expression.Convert(param, type);
            var propertyAccess = System.Linq.Expressions.Expression.Property(cast, prop);
            var convertResult = System.Linq.Expressions.Expression.Convert(propertyAccess, typeof(object));
            accessor = System.Linq.Expressions.Expression.Lambda<Func<object, object>>(convertResult, param).Compile();

            _cache[key] = accessor;
        }

        return accessor(obj)?.ToString();
    }
}
