using System.Diagnostics;
using AzotBase.Page.Header;
using AzotBase.Utils;

namespace AzotBase.Page;

public abstract class PageBase : IPage<PageBase>
{
    public abstract int Id { get; }
    public byte IsDirty { get; set; }
    private readonly AsyncReaderWriterLock _pageLock = new AsyncReaderWriterLock();
    
    public abstract byte[] ToByteArray();
    
    public async Task EnterReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        await _pageLock.EnterReadLock(millisecondsTimeout);
    }

    public async Task ExitReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        await _pageLock.ExitReadLock(millisecondsTimeout);
    }

    public async Task EnterWriteLock(int millisecondsTimeout = Timeout.Infinite)
    {
        LockTracker.WaitingFor(Id);
        await _pageLock.EnterWriteLock(millisecondsTimeout);
        LockTracker.Acquired(Id);
    }

    public async Task ExitWriteLock(int millisecondsTimeout = Timeout.Infinite)
    {
        await _pageLock.ExitWriteLock(millisecondsTimeout);
        LockTracker.Released(Id);
    }

    public async Task<bool> TryUpgradeReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        LockTracker.WaitingFor(Id);
        var result = await _pageLock.TryUpgradeReadLock(millisecondsTimeout);
        if (result)
            LockTracker.Acquired(Id);
        else
            LockTracker.CancelWait();
        return result;
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