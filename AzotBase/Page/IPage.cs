using AzotBase.Page.Header;

namespace AzotBase.Page;

public interface IPage
{
    public byte[] ToByteArray();
}

public interface IPage<out T> : IPage
{
    public static abstract T FromByteArray(Span<byte> bytes);
}

