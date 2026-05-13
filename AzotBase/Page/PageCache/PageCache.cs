using System.Diagnostics.CodeAnalysis;
using AzotBase.Page.PageCache.PageCacheEventArgs;
using AzotBase.Utils.Collections.Lru;

namespace AzotBase.Page.PageCache;

public class PageCache<K> where K : notnull
{
    public readonly int Capacity;
    public event EventHandler<DeleteEventArgs<K, PageBase>>? DeleteEvent;

    private readonly LruCache<K, PageBase> _lruCache;

    private readonly SemaphoreSlim _pinSemaphore = new SemaphoreSlim(1, 1);

    public PageCache(int capacity)
    {
        _lruCache = new LruCache<K, PageBase>(capacity);
        _lruCache.OnEvicted += OnEvictedEvent;
        Capacity = capacity;
    }
    
    public bool TryGetValue(
        K key, 
        [NotNullWhen(true)] out PageBase? value, 
        bool makePin = false)
    {
        if (!makePin)
            return _lruCache.TryGetValue(key, out value);

        value = _lruCache.Pin(key);
        
        return value != null;
    }
    
    public async Task<bool> TryAddAsync(
        K key, 
        PageBase value, 
        bool makePin = false)
    {
        if (!makePin)
            return await _lruCache.TryAddAsync(key, value);
        
        await _pinSemaphore.WaitAsync();
        
        await _lruCache.TryAddAsync(key, value);
        _lruCache.Pin(key);
        
        _pinSemaphore.Release();
        
        return true;
    }

    public void UnpinPage(K pageId) => _lruCache.Unpin(pageId);
    
    private void OnEvictedEvent(K key, PageBase page)
        => DeleteEvent?.Invoke(this, new DeleteEventArgs<K, PageBase>(key, page));
}