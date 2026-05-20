using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using AzotBase.Common.Serialization.Attributes;

namespace AzotBase.Common.Serialization;

public static class TypeMetadataCache
{
    private static readonly Dictionary<Type, PropertyAccessor[]> _classMembers = [];
    private static readonly Dictionary<Type, int> _structSizes = [];

    public static PropertyAccessor[] GetProperties<T>()
        => GetProperties(typeof(T));
    
    public static PropertyAccessor[] GetProperties(Type type)
    {
        if (_classMembers.TryGetValue(type, out var accessors))
            return accessors;
        
        var props = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .OrderBy(p => p.GetCustomAttribute<SerializeOrderAttribute>()?.Order ?? int.MaxValue)
            .Select(CreatePropertyAccessor)
            .ToArray();
        
        _classMembers[type] = props;
        return props;
    }
    
    public static int GetStructSize(Type type)
    {
        if (_structSizes.TryGetValue(type, out var size))
            return size;
        
        var method = typeof(Unsafe).GetMethod("SizeOf") 
                     ?? throw new InvalidOperationException("Unsafe doesn't have SizeOf method");
        
        var result = method.MakeGenericMethod(type).Invoke(null, null)
                     ?? throw new Exception("Null structure size");
        
        _structSizes[type] = (int)result;
        
        return (int)result;
    }

    private static PropertyAccessor CreatePropertyAccessor(PropertyInfo property)
    {
        var obj = Expression.Parameter(typeof(object), "obj");
        var value = Expression.Parameter(typeof(object), "value");
        var castExp = Expression.Convert(obj, property.DeclaringType!);
        
        var getter = Expression.Lambda<Func<object, object?>>(
            Expression.Convert(
                Expression.Property(castExp, property), typeof(object)),
            obj)
            .Compile();

        var setter = Expression.Lambda<Action<object, object?>>(
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
    public required Func<object, object?> Getter { get; init; }
    public required Action<object, object?> Setter { get; init; }
}