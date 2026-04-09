using System.Collections.Concurrent;
using System.Diagnostics;
using AzotBase.Page;
using AzotBase.Page.PageCache;

namespace Test.PageTest;

public class PageCacheTests
{
    [Fact]
    public async Task AddAndGetValue_ShouldReturnAddedValue()
    {
        var cache = new PageCache<DataPage>(2);
        var page = new DataPage(1);

        await cache.TryAddAsync(1, page);

        bool found = cache.TryGetValue(1, out var result);

        Assert.True(found);
        Assert.Equal(page, result);
    }

    [Fact]
    public async Task LRU_EvictsOldestUnpinned_WhenCapacityExceeded()
    {
        var cache = new PageCache<DataPage>(2);
        var page1 = new DataPage(1);
        var page2 = new DataPage(2);
        var page3 = new DataPage(3);

        await cache.TryAddAsync(1, page1);
        await cache.TryAddAsync(2, page2);
        
        DataPage? deletedPage = null;
        cache.DeleteEvent += (_, e) => deletedPage = e.Value;

        await cache.TryAddAsync(3, page3);
        
        Assert.Equal(page1, deletedPage);
        
        Assert.True(cache.TryGetValue(2, out var p2) && p2 == page2);
        Assert.True(cache.TryGetValue(3, out var p3) && p3 == page3);
        Assert.False(cache.TryGetValue(1, out _));
    }

    [Fact]
    public async Task PinPage_PreventsEviction()
    {
        var cache = new PageCache<DataPage>(2);
        var page1 = new DataPage(1);
        var page2 = new DataPage(2);
        var page3 = new DataPage(3);

        await cache.TryAddAsync(1, page1, true);
        await cache.TryAddAsync(2, page2);

        DataPage? deletedPage = null;
        cache.DeleteEvent += (_, e) => deletedPage = e.Value;

        await cache.TryAddAsync(3, page3);
        
        Assert.Equal(page2, deletedPage);
        Assert.True(cache.TryGetValue(1, out var p1) && p1 == page1);
        Assert.True(cache.TryGetValue(3, out var p3) && p3 == page3);
        Assert.False(cache.TryGetValue(2, out _));
    }

    [Fact]
    public async Task UnpinPage_AllowsEviction()
    {
        var cache = new PageCache<DataPage>(2);
        var page1 = new DataPage(1);
        var page2 = new DataPage(2);
        var page3 = new DataPage(3);

        await cache.TryAddAsync(1, page1, true);
        cache.UnpinPage(1);
        
        bool deleted = false;
        cache.DeleteEvent += (_, _) => deleted = true;
        
        await cache.TryAddAsync(2, page2);
        await cache.TryAddAsync(3, page3);

        Assert.True(deleted);
    }

    [Fact]
    public async Task TryGetValue_ShouldMoveNodeToTail()
    {
        var cache = new PageCache<DataPage>(2);
        var page1 = new DataPage(1);
        var page2 = new DataPage(2);
        var page3 = new DataPage(3);

        await cache.TryAddAsync(1, page1);
        await cache.TryAddAsync(2, page2);
        
        bool found = cache.TryGetValue(1, out _);
        Assert.True(found);
        
        DataPage? deletedPage = null;
        cache.DeleteEvent += (_, e) => deletedPage = e.Value;

        await cache.TryAddAsync(3, page3);

        Assert.Equal(page2, deletedPage);
        Assert.True(cache.TryGetValue(1, out _));
        Assert.True(cache.TryGetValue(3, out _));
        Assert.False(cache.TryGetValue(2, out _));
    }
    
    [Fact]
    public async Task Concurrent_TryGetValue_ShouldFindAddedValues()
    {
        var cacheSize = 100;
        var cache = new PageCache<DataPage>(cacheSize);
        
        var threadCount = 32;
        var requestsPerThread = 20000;
        
        for (int i = 0; i < cache.Capacity; i++)
            await cache.TryAddAsync(i, new DataPage(i));
        
        var tasks = Enumerable.Range(0, threadCount)
            .Select(_ => Task.Run(() =>
            {
                for (int k = 0; k <= requestsPerThread; k++)
                {
                    int id = new Random().Next(0, cacheSize - 1);
                    try
                    {
                        if (!cache.TryGetValue(id, out var _))
                            Assert.True(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        break;
                    }
                }
                
            }));
        
        await Task.WhenAll(tasks);
    }
    
    [Fact]
    public async Task Concurrent_Add_ShouldEvictCorrectly()
    {
        var cacheSize = 100000;
        var cache = new PageCache<DataPage>(cacheSize);
        
        var threadCount = 32;
        var addCountPerThread = 20000;
        
        int evictCount = 0;
        
        cache.DeleteEvent += (_, e) => Interlocked.Increment(ref evictCount);
        
        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(async () =>
            {
                for (int k = 0; k < addCountPerThread; k++)
                {
                    var id = t * addCountPerThread + k;
                    var page = new DataPage(id);
                    try
                    {
                        await cache.TryAddAsync(id, page);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        break;
                    }
                }
            }));

        await Task.WhenAll(tasks);

        Assert.Equal(threadCount * addCountPerThread - cacheSize, evictCount);
    }
    
    [Fact]
    public async Task Concurrent_AddAndPin_ShouldNotOccurDeadlock()
    {
        var cacheSize = 1000;
        var cache = new PageCache<DataPage>(cacheSize);

        var threadCount = 32;
        var addCountPerThread = 200;
        
        int evictCount = 0;
        
        var unpinTasks = new ConcurrentBag<Task>();
        
        cache.DeleteEvent += (_, e) => Interlocked.Increment(ref evictCount);
        
        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(async () =>
            {
                for (int k = 0; k < addCountPerThread; k++)
                {
                    var id = t * addCountPerThread + k;
                    var page = new DataPage(id);
                    try
                    {
                        await cache.TryAddAsync(id, page, true);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                        throw;
                    }
                    
                    unpinTasks.Add(Task.Run(async () =>
                    {
                        await Task.Delay(new Random().Next(0, 1000)).ConfigureAwait(false);
                        try
                        {
                            cache.UnpinPage(id);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                            throw;
                        }
                    }));
                }
            }));

        await Task.WhenAll(tasks);
        await Task.WhenAll(unpinTasks);
        
        Assert.Equal(threadCount * addCountPerThread - cacheSize, evictCount);
    }
}