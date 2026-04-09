using System.Diagnostics;
using AzotBase.Page.Header;
using AzotBase.Utils;
using AzotBase.Utils.LockUtils;

namespace AzotBase.Page;

public abstract class PageBase : IPage<PageBase>
{
    public abstract int Id { get; }
    public byte IsDirty { get; set; }
    private readonly IReaderWriterLock _pageLock;

    protected PageBase(bool asyncLock = false)
    {
        if (asyncLock)
            _pageLock = new AsyncReaderWriterLock();
        else
            _pageLock = new ReaderWriterLockAdapter();
    }
    
    public abstract byte[] ToByteArray();
    
    public async ValueTask EnterReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        await _pageLock.EnterReadLock(millisecondsTimeout);
    }

    public async ValueTask ExitReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        await _pageLock.ExitReadLock(millisecondsTimeout);
    }

    public async ValueTask EnterWriteLock(int millisecondsTimeout = Timeout.Infinite)
    {
        await _pageLock.EnterWriteLock(millisecondsTimeout);
    }

    public async ValueTask ExitWriteLock(int millisecondsTimeout = Timeout.Infinite)
    {
        await _pageLock.ExitWriteLock(millisecondsTimeout);
    }

    public async ValueTask<bool> TryUpgradeReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        return await _pageLock.TryUpgradeReadLock(millisecondsTimeout);
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