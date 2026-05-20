namespace AzotBase.Common.Serialization;

public static class ClassSerializer
{
    public static byte[] Serialize<T>(T obj) where T : notnull
        => Serialize(typeof(T), obj);

    public static byte[] Serialize(Type type, object obj)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);

        SerializeObjectMembers(type, obj, writer);
        writer.Flush();
        
        return ms.ToArray();
    }
    
    /// <summary>
    /// Should have parameterless constructor
    /// </summary>
    public static T Deserialize<T>(byte[] data)
        => (T)Deserialize(typeof(T), data);

    public static object Deserialize(Type type, byte[] data)
    {
        var reader = new BinaryReader(new MemoryStream(data));
        
        return DeserializeObjectMembers(type, reader);
    }

    private static void SerializeObject(Type type, object? obj, BinaryWriter writer)
    {
        if (obj is null)
        {
            writer.Write(false);
            return;
        }
        writer.Write(true);
        
        var notNullableType = Nullable.GetUnderlyingType(type) ?? type;
        if (TrySerializeBuiltInType(notNullableType, obj, writer))
            return;

        SerializeObjectMembers(type, obj, writer);
    }

    private static void SerializeObjectMembers(Type type, object obj, BinaryWriter writer)
    {
        var props = TypeMetadataCache.GetProperties(type);
        foreach (var accessor in props)
        {
            var prop = accessor.Getter.Invoke(obj);
            SerializeObject(accessor.Type, prop, writer);
        }
    }

    private static object? DeserializeObject(Type type, BinaryReader reader)
    {
        bool hasValue = reader.ReadBoolean();
        if (!hasValue)
            return null;

        var notNullableType = Nullable.GetUnderlyingType(type) ?? type;
        if (TryDeserializeBuiltInType(notNullableType, reader, out var obj))
            return obj;

        return DeserializeObjectMembers(type, reader);
    }

    private static object DeserializeObjectMembers(Type type, BinaryReader reader)
    {
        var props = TypeMetadataCache.GetProperties(type);
        var result = Activator.CreateInstance(type) 
                     ?? throw new InvalidOperationException("Could not create instance of type " + type.FullName);
        
        foreach (var accessor in props)
        {
            var deserializedValue = DeserializeObject(accessor.Type, reader);
            accessor.Setter.Invoke(result, deserializedValue);
        }

        return result;
    }

    private static bool TrySerializeStructure(Type type, object obj, BinaryWriter writer)
    {
        if (!type.IsValueType)
            return false;
        
        var serialized = StructSerializer.Serialize(type, obj);
        writer.Write(serialized);
        return true;
    }

    private static bool TryDeserializeStructure(Type type, BinaryReader reader, out object? obj)
    {
        if (!type.IsValueType)
        {
            obj = null;
            return false;
        }
        
        var structBytes = reader.ReadBytes(TypeMetadataCache.GetStructSize(type));
        obj = StructSerializer.Deserialize(type, structBytes);
        return true;
    }

    private static bool TrySerializeBuiltInType(Type type, object obj, BinaryWriter writer)
    {
        if (type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(type);
            obj = Convert.ChangeType(obj, underlyingType);
        }
        
        switch (obj)
        {
            case int s: writer.Write(s); return true;
            case uint s: writer.Write(s); return true;
            case short s: writer.Write(s); return true;
            case ushort s: writer.Write(s); return true;
            case long l: writer.Write(l); return true;
            case ulong l: writer.Write(l); return true;
            case float f: writer.Write(f); return true;
            case double d: writer.Write(d); return true;
            case bool b: writer.Write(b); return true;
            case string s: writer.Write(s); return true;
            case byte b: writer.Write(b); return true;
            case sbyte b: writer.Write(b); return true;
            case decimal d: writer.Write(d); return true;
            case char c: writer.Write(c); return true;
        }
        
        if (TrySerializeStructure(type, obj, writer))
            return true;
        
        return false;
    }
    
    private static bool TryDeserializeBuiltInType(Type type, BinaryReader reader, out object? obj)
    {
        if (type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(type);
            TryDeserializeBuiltInType(underlyingType, reader, out var enumValue);
            if (enumValue == null)
                throw new Exception("Could not deserialize Enum underlyingType");
            
            obj = Enum.ToObject(type, enumValue);
            return true;
        }
        
        obj = Type.GetTypeCode(type) switch
        {
            TypeCode.Int32   => reader.ReadInt32(),
            TypeCode.UInt32  => reader.ReadUInt32(),
            TypeCode.Int16   => reader.ReadInt16(),
            TypeCode.UInt16  => reader.ReadUInt16(),
            TypeCode.Int64   => reader.ReadInt64(),
            TypeCode.UInt64  => reader.ReadUInt64(),
            TypeCode.Single  => reader.ReadSingle(),
            TypeCode.Double  => reader.ReadDouble(),
            TypeCode.Boolean => reader.ReadBoolean(),
            TypeCode.String  => reader.ReadString(),
            TypeCode.Byte    => reader.ReadByte(),
            TypeCode.SByte   => reader.ReadSByte(),
            TypeCode.Decimal => reader.ReadDecimal(),
            TypeCode.Char    => reader.ReadChar(),
            _                => null
        };
        if (obj != null)
            return true;
        
        if (TryDeserializeStructure(type, reader, out obj))
            return true;

        return obj != null;
    }
}