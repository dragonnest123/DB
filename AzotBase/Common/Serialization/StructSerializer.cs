using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AzotBase.Common.Serialization;

/// <summary>
/// Only for structures that do not contain reference types
/// </summary>
public static class StructSerializer 
{
    public static byte[] Serialize<T>(ref T value) where T : unmanaged
    {
        var span = MemoryMarshal.CreateSpan(ref value, 1);
        return MemoryMarshal.AsBytes(span).ToArray();
    }

    public static void Serialize<T>(byte[] buffer, int offset, ref T value) where T : unmanaged
    {
        var span = MemoryMarshal.CreateSpan(ref value, 1);
        var bytes = MemoryMarshal.AsBytes(span);
        bytes.CopyTo(buffer.AsSpan(offset));
    }
    
    public static byte[] Serialize(Type type, object value)
    {
        var size = TypeMetadataCache.GetStructSize(type);

        var buffer = new byte[size];
        var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
        try
        {
            Marshal.Copy(handle.AddrOfPinnedObject(), buffer, 0, size);
        }
        finally
        {
            handle.Free();
        }
        
        return buffer;
    }
    
    public static T Deserialize<T>(Span<byte> data) where T : unmanaged
    {
        return MemoryMarshal.Read<T>(data);
    }
    
    public static object? Deserialize(Type type, byte[] data)
    {
        var result = Activator.CreateInstance(type);
        var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
        try
        {
            Marshal.Copy(data, 0, handle.AddrOfPinnedObject(), data.Length);
            return result;
        }
        finally
        {
            handle.Free();
        }
    }
}