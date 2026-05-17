using System.Linq.Expressions;
using System.Reflection;

namespace AzotBase.Common.Serialization;

public static class TypeMetadataCache
{
    private static readonly Dictionary<Type, PropertyAccessor[]> _cache = [];

    public static PropertyAccessor[] GetProperties<T>()
    {
        if (_cache.TryGetValue(typeof(T), out var accessors))
            return accessors;
        
        var props = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(CreatePropertyAccessor)
            .ToArray();
        
        _cache[typeof(T)] = props;
        return props;
    }

    private static PropertyAccessor CreatePropertyAccessor(PropertyInfo property)
    {
        var obj = Expression.Parameter(typeof(object), "obj");
        var value = Expression.Parameter(typeof(object), "value");
        var castExp = Expression.Convert(obj, property.DeclaringType);
        
        var getter = Expression.Lambda<Func<object, object>>(
            Expression.Convert(
                Expression.Property(castExp, property), typeof(object)),
            obj)
            .Compile();

        var setter = Expression.Lambda<Action<object, object>>(
            Expression.Assign(
                Expression.Property(castExp, property),
                Expression.Convert(value, property.PropertyType)),
            obj, value)
            .Compile();

        return new PropertyAccessor
        {
            Type = property.PropertyType,
            Getter = getter,
            Setter = setter
        };
    }
}

public class PropertyAccessor
{
    public required Type Type { get; init; }
    public required Func<object, object> Getter { get; init; }
    public required Action<object, object> Setter { get; init; }
}