using System.Diagnostics;
using AzotBase.Page.Header;
using AzotBase.Utils;

namespace AzotBase.Page;

public abstract class PageBase : IPage<PageBase>
{
    public byte IsDirty { get; set; }
    private readonly AsyncReaderWriterLock _pageLock = new AsyncReaderWriterLock();
    
    public abstract byte[] ToByteArray();
    
    public async Task EnterReadLock()
    {
        await _pageLock.EnterReadLock();
    }

    public async Task ExitReadLock()
    {
        await _pageLock.ExitReadLock();
    }

    public async Task EnterWriteLock()
    {
        await _pageLock.EnterWriteLock();
    }

    public async Task ExitWriteLock()
    {
        await _pageLock.ExitWriteLock();
    }

    public async Task<bool> TryUpgradeReadLock()
    {
        return await _pageLock.TryUpgradeReadLock();
    }

    public static PageBase FromByteArray(Span<byte> bytes)
    {
        var type = (PageType)BitConverter.ToInt16(bytes[4..6]);

        return type switch
        {
            PageType.IndexPage => IndexPage.FromByteArray(bytes),
            PageType.LeafPage => LeafPage.FromByteArray(bytes),
            PageType.DataPage => DataPage.FromByteArray(bytes),
            PageType.SystemPage => SystemPage.FromByteArray(bytes),
            PageType.OverflowPage => throw new NotImplementedException(),
            _ => throw new NotImplementedException("Not supported page type")
        };
    }

    public static PageBase CreateEmpty(int id)
    {
        throw new NotImplementedException("Should be implemented in derived classes");
    }
}