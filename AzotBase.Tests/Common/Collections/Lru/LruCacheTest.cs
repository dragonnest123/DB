using AzotBase.Common.Collections.Lru;

namespace AzotName.Tests.Common.Collections.Lru;

public class LruCacheTest
{
    [Fact]
    public async Task AddAndGetValue_ShouldReturnAddedValue()
    {
        var cache = new LruCache<int, int>(2);

        await cache.TryAddAsync(1, 1);

        bool found = cache.TryGetValue(1, out var result);

        Assert.True(found);
        Assert.Equal(1, result);
    }
    
    [Fact]
    public async Task LRU_EvictsOldest_WhenCapacityExceeded()
    {
        int evictedKey = -1;
        var cache = new LruCache<int, int>(2);
        cache.OnEvicted += (k, _) => evictedKey = k;

        await cache.TryAddAsync(1, 1);
        await cache.TryAddAsync(2, 2);
        await cache.TryAddAsync(3, 3);

        Assert.Equal(1, evictedKey);
        Assert.False(cache.TryGetValue(1, out _));
        Assert.True(cache.TryGetValue(2, out _));
        Assert.True(cache.TryGetValue(3, out _));
    }
    
    [Fact]
    public async Task TryGetValue_ShouldMoveNodeToTail()
    {
        int evictedKey = -1;
        var cache = new LruCache<int, int>(2);
        cache.OnEvicted += (k, _) => evictedKey = k;

        await cache.TryAddAsync(1, 1);
        await cache.TryAddAsync(2, 2);
        cache.TryGetValue(1, out _);
        await cache.TryAddAsync(3, 3);

        Assert.Equal(2, evictedKey);
    }
    
    [Fact]
    public async Task Pin_PreventsEviction()
    {
        int evictedKey = -1;
        var cache = new LruCache<int, int>(2);
        cache.OnEvicted += (k, _) => evictedKey = k;

        await cache.TryAddAsync(1, 1);
        cache.Pin(1);
        await cache.TryAddAsync(2, 2);
        await cache.TryAddAsync(3, 3);

        Assert.Equal(2, evictedKey);
    }

    [Fact]
    public async Task Unpin_AllowsEviction()
    {
        int evictedKey = -1;
        var cache = new LruCache<int, int>(2);
        cache.OnEvicted += (k, _) => evictedKey = k;

        await cache.TryAddAsync(1, 1);
        cache.Pin(1);
        cache.Unpin(1);
        await cache.TryAddAsync(2, 2);
        await cache.TryAddAsync(3, 3);

        Assert.Equal(1, evictedKey);
    }
    
    [Fact]
    public async Task Concurrent_Add_ShouldEvictCorrectly()
    {
        var cacheSize = 1000;
        var cache = new LruCache<int, int>(cacheSize);
        
        var threadCount = 32;
        var addCountPerThread = 20000;
        
        int evictCount = 0;
        cache.OnEvicted += (_, _) => Interlocked.Increment(ref evictCount);
        
        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(async () =>
            {
                for (int k = 0; k < addCountPerThread; k++)
                    await cache.TryAddAsync(t * addCountPerThread + k, k);
            }));

        await Task.WhenAll(tasks);

        Assert.Equal(threadCount * addCountPerThread - cacheSize, evictCount);
    }

    [Fact]
    public async Task Concurrent_TryGetValue_ShouldFindAddedValues()
    {
        var cacheSize = 1000;
        var cache = new LruCache<int, int>(cacheSize);
        
        var threadCount = 32;
        var tryGetPerThread = 20000;
        
        for (int i = 0; i < cacheSize; i++)
            await cache.TryAddAsync(i, i);

        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(() =>
            {
                for (int k = 0; k < tryGetPerThread; k++)
                {
                    int id = new Random().Next(0, cacheSize - 1);
                    Assert.True(cache.TryGetValue(id, out _));
                }
            }));

        await Task.WhenAll(tasks);
    }
}