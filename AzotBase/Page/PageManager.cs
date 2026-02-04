using AzotBase.Utils;

namespace AzotBase.Page;

public class PageManager
{
    private readonly LRUCache<int, DataPage> _cache = new LRUCache<int, DataPage>(1000);
    private readonly FileStream _fileStream;

    public PageManager(FileStream fileStream)
    {
        _fileStream = fileStream;
    }

    public async Task<DataPage> ReadDataPage(int pageId)
    {
        if (_cache.TryGetValue(pageId, out DataPage? cached))
            return cached;
        
        var page = await ReadPage<DataPage>(pageId);
        _cache.Add(pageId, page);
        
        return page;
    }

    public async Task<PageType> ReadPageType(int pageId)
    {
        var typeBytes = new byte[2];
        
        _fileStream.Position = SystemPage.PageSize * pageId + sizeof(int);
        await _fileStream.ReadExactlyAsync(typeBytes, 0, typeBytes.Length);
        
        return (PageType)BitConverter.ToInt16(typeBytes, 0);
    }
    
    public async Task<T> ReadPage<T>(int pageId) where T : IPage<T>
    {
        var bytes = new byte[SystemPage.PageSize];
        _fileStream.Position = SystemPage.PageSize * pageId;
        await _fileStream.ReadExactlyAsync(bytes, 0, SystemPage.PageSize);
        
        return T.FromByteArray(bytes);
    }
    
    public async Task WritePage<T>(T page, int pageId) where T : IPage<T>
    {
        var pageBytes = page.ToByteArray();
        _fileStream.Position = SystemPage.PageSize * pageId;
        await _fileStream.WriteAsync(pageBytes);
    }
}