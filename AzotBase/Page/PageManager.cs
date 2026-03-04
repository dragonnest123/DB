using AzotBase.Utils;

namespace AzotBase.Page;

public enum PageLockMode
{
    ReadLock,
    WriteLock,
    NoLock
}

public class PageManager
{
    private readonly PageCache<PageBase> _cache = new PageCache<PageBase>(100000);
    private readonly FileStream _fileStream;
    private readonly Queue<int> _freePages = new Queue<int>();

    public PageManager(FileStream fileStream)
    {
        _fileStream = fileStream;
        _cache.DeleteEvent += async (_, args) =>
        {
            var page = args.Value;
            if (page.IsDirty == 1)
                await WritePage(args.Value, args.Key);
        };
    }

    public async Task<T> AllocatePage<T>(PageLockMode lockMode = PageLockMode.NoLock) where T : PageBase, IPage<T>
    {
        var pageId = AllocatePageId();
        var page = T.CreateEmpty(pageId);
        
        await LockPage(page, lockMode);
        
        await WritePage(page, pageId);
        
        await _cache.AddAsync(pageId, page);

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
    
    public async Task<T> LoadPage<T>(int pageId, PageLockMode lockMode = PageLockMode.NoLock) where T : PageBase, IPage<T>
    {
        if (_cache.TryGetValue(pageId, out PageBase? cached))
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
        
        await _cache.AddAsync(pageId, page);

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

    public void PinPage(int pageId) => _cache.PinPage(pageId);
    
    public void UnpinPage(int pageId) => _cache.UnpinPage(pageId);
    
    private int AllocatePageId()
    {
        if (_freePages.Count > 0)
            return _freePages.Dequeue();

        return (int)(_fileStream.Length / SystemPage.PageSize);
    }

    private async Task LockPage(PageBase page, PageLockMode lockMode)
    {
        switch (lockMode)
        {
            case PageLockMode.ReadLock: await page.EnterReadLock();
                break;
            case PageLockMode.WriteLock: await page.EnterWriteLock();
                break;
            case PageLockMode.NoLock:
                break;
            default: throw new ArgumentOutOfRangeException();
        }
    }
}