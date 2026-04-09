
namespace AzotBase.Page;

public interface IPage
{
    public byte IsDirty { get; set; }
    public byte[] ToByteArray();
}

public interface IPage<out T> : IPage
{
    public static abstract T FromByteArray(Span<byte> bytes);
    public static abstract T CreateEmpty(int id);
}

