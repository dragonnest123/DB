using System.Runtime.InteropServices;
using AzotBase.Page;

namespace AzotBase.Utils;

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

    public static T Deserialize<T>(Span<byte> data) where T : unmanaged
    {
        return MemoryMarshal.Read<T>(data);
    }
}