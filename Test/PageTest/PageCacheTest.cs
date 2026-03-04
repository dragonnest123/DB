using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzotBase.Page;
using Xunit;

namespace Test.PageTest;

public class PageCacheTests
{
    [Fact]
    public async Task AddAndGetValue_ShouldReturnAddedValue()
    {
        var cache = new PageCache<DataPage>(2);
        var page = new DataPage(1);

        await cache.AddAsync(1, page);

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

        await cache.AddAsync(1, page1);
        await cache.AddAsync(2, page2);
        
        DataPage? deletedPage = null;
        cache.DeleteEvent += (_, e) => deletedPage = e.Value;

        await cache.AddAsync(3, page3);
        
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

        await cache.AddAsync(1, page1);
        await cache.AddAsync(2, page2);

        cache.PinPage(1);

        DataPage? deletedPage = null;
        cache.DeleteEvent += (_, e) => deletedPage = e.Value;

        await cache.AddAsync(3, page3);
        
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

        await cache.AddAsync(1, page1);
        cache.PinPage(1);
        cache.UnpinPage(1);
        
        bool deleted = false;
        cache.DeleteEvent += (_, _) => deleted = true;
        
        await cache.AddAsync(2, page2);
        await cache.AddAsync(3, page3);

        Assert.True(deleted);
    }

    [Fact]
    public async Task TryGetValue_ShouldMoveNodeToTail()
    {
        var cache = new PageCache<DataPage>(2);
        var page1 = new DataPage(1);
        var page2 = new DataPage(2);
        var page3 = new DataPage(3);

        await cache.AddAsync(1, page1);
        await cache.AddAsync(2, page2);
        
        bool found = cache.TryGetValue(1, out _);
        Assert.True(found);
        
        DataPage? deletedPage = null;
        cache.DeleteEvent += (_, e) => deletedPage = e.Value;

        await cache.AddAsync(3, page3);

        Assert.Equal(page2, deletedPage);
        Assert.True(cache.TryGetValue(1, out _));
        Assert.True(cache.TryGetValue(3, out _));
        Assert.False(cache.TryGetValue(2, out _));
    }
    
    [Fact]
    public async Task Concurrent_AddAndGetValueWith_ShouldReturnAddedValue()
    {
        var cacheSize = 1000;
        var cache = new PageCache<DataPage>(cacheSize);

        var threadCount = 32;
        var addCountPerThread = 10000;
        
        ConcurrentBag<bool> results = new ConcurrentBag<bool>();
        
        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(async () =>
            {
                for (int k = 0; k <= addCountPerThread; k++)
                {
                    var id = t * addCountPerThread + k;
                    var page = new DataPage(id);
                    await cache.AddAsync(id, page);
                    results.Add(cache.TryGetValue(id, out _));
                }
            }));

        await Task.WhenAll(tasks);

        Assert.All(results, Assert.True);
    }
}
