using System.Text;

namespace AzotBase.Utils;

public static class ClassSerializer
{
    public static byte[] Serialize<T>(T obj)
    {
        var props = TypeMetadataCache.GetProperties<T>();
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);

        foreach (var accessor in props)
        {
            var prop = accessor.Getter.Invoke(obj);
            SerializePrimitiveType(prop, writer);
        }
        writer.Flush();
        
        return ms.ToArray();
    }
    
    /// <summary>
    /// Should have parameterless constructor
    /// </summary>
    public static T Deserialize<T>(byte[] data) where T : new()
    {
        var props = TypeMetadataCache.GetProperties<T>();
        var result = new T();
        var reader = new BinaryReader(new MemoryStream(data));

        foreach (var accessor in props)
            accessor.Setter.Invoke(result, DeserializePrimitiveType(accessor.Type, reader));
        
        return result;
    }

    private static void SerializePrimitiveType(object obj, BinaryWriter writer)
    {
        if (obj == null)
        {
            writer.Write(false);
            return;
        }
        writer.Write(true); 
        
        switch (obj)
        {
            case int s: writer.Write(s); break;
            case uint s: writer.Write(s); break;
            case short s: writer.Write(s); break;
            case ushort s: writer.Write(s); break;
            case long l: writer.Write(l); break;
            case ulong l: writer.Write(l); break;
            case float f: writer.Write(f); break;
            case double d: writer.Write(d); break;
            case bool b: writer.Write(b); break;;
            case string s: writer.Write(s); break;
            default: throw new ArgumentException("Object has complex type");
        }
    }
    
    private static object? DeserializePrimitiveType(Type type, BinaryReader reader)
    {
        bool hasValue = reader.ReadBoolean();
        if (!hasValue)
            return null;
        
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Int32: return reader.ReadInt32();
            case TypeCode.UInt32: return reader.ReadUInt32();
            case TypeCode.Int16: return reader.ReadInt16();
            case TypeCode.UInt16: return reader.ReadUInt16();
            case TypeCode.Int64: return reader.ReadInt64();
            case TypeCode.UInt64: return reader.ReadUInt64();
            case TypeCode.Single: return reader.ReadSingle();
            case TypeCode.Double: return reader.ReadDouble();
            case TypeCode.Boolean: return reader.ReadBoolean();
            case TypeCode.String: return reader.ReadString();
            default: throw new ArgumentException("Object has complex type");
        }
    }
}