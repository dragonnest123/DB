using System.Collections.Concurrent;
using AzotBase.Storage.Cache;
using AzotBase.Storage.Pages;
using AzotBase.Storage.Pages.Common;

namespace AzotBase.Storage;

public enum PageLockMode
{
    ReadLock,
    WriteLock,
    NoLock
}

public class PageManager
{
    private readonly PageCache<int> _cache = new PageCache<int>(10000);
    private readonly FileStream _fileStream;
    private readonly ConcurrentQueue<int> _freePages = new ConcurrentQueue<int>();
    private int _nextPageId;

    public PageManager(FileStream fileStream)
    {
        _fileStream = fileStream;
        _nextPageId = (int)_fileStream.Length / SystemPage.PageSize;
        _cache.DeleteEvent += async (_, args) =>
        {
            var page = args.Value;
            if (page.IsDirty == 1)
                await WritePage(args.Value, args.Key);
        };
    }

    public async Task<T> AllocatePage<T>(
        bool pinPage,
        PageLockMode lockMode = PageLockMode.NoLock) where T : PageBase, IPage<T>
    {
        var pageId = AllocatePageId();
        var page = T.CreateEmpty(pageId);
        
        await LockPage(page, lockMode);
        
        await WritePage(page, pageId);
        
        await _cache.TryAddAsync(pageId, page, pinPage);

        return page;
    }
    
    public async Task<PageType> ReadPageType(int pageId)
    {
        var typeBytes = new byte[2];
        
        await RandomAccess.ReadAsync(
            _fileStream.SafeFileHandle, 
            typeBytes, 
            SystemPage.PageSize * pageId + sizeof(int));
        
        return (PageType)BitConverter.ToInt16(typeBytes, 0);
    }
    
    public async Task<T> LoadPage<T>(
        int pageId, 
        bool pinPage,
        PageLockMode lockMode = PageLockMode.NoLock) where T : PageBase, IPage<T>
    {
        if (_cache.TryGetValue(pageId, out PageBase? cached, pinPage))
        {
            await LockPage(cached, lockMode);
            return (T)cached;
        }
        
        var bytes = new byte[SystemPage.PageSize];
        
        await RandomAccess.ReadAsync(
            _fileStream.SafeFileHandle, 
            bytes, 
            pageId * SystemPage.PageSize);
        
        var page = T.FromByteArray(bytes);
        
        await LockPage(page, lockMode);

        if (!await _cache.TryAddAsync(pageId, page, pinPage))
            page = await LoadPage<T>(pageId, pinPage, lockMode);

        return page;
    }
    
    public async Task WritePage<T>(T page, int pageId) where T : IPage
    {
        var pageBytes = page.ToByteArray();
        
        await RandomAccess.WriteAsync(
            _fileStream.SafeFileHandle, 
            pageBytes, 
            pageId * SystemPage.PageSize);

        page.IsDirty = 0;
    }
    
    public void UnpinPage(int pageId) => _cache.UnpinPage(pageId);
    
    private int AllocatePageId()
    {
        if (_freePages.TryDequeue(out var pageId))
            return pageId;

        return Interlocked.Increment(ref _nextPageId) - 1;
    }

    private static ValueTask LockPage(PageBase page, PageLockMode lockMode)
    {
        switch (lockMode)
        {
            case PageLockMode.ReadLock:
                return page.EnterReadLock(1000);
            case PageLockMode.WriteLock:
                return page.EnterWriteLock(1000);
            case PageLockMode.NoLock:
                return ValueTask.CompletedTask;
            default: throw new ArgumentOutOfRangeException(nameof(lockMode));
        }
    }
}