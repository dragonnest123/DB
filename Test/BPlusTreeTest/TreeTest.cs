using System.Collections.Concurrent;
using System.Diagnostics;
using AzotBase.Page;
using AzotBase.Tree;
using AzotBase.Utils;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Test.BPlusTreeTest;

public class TreeTest
{
    private readonly PageManager _pageManager;
    private readonly BPlusTree _tree;
    private readonly int _rootPageId;

    public TreeTest()
    {
        _pageManager = PageManagerFactory.Create();
        
        var root =  _pageManager.AllocatePage<LeafPage>().Result;
        _rootPageId = root.Header.Id;
        _tree = new BPlusTree(_pageManager, _rootPageId);
    }
    
    [Fact]
    public async Task Insert_SingleKey_ShouldAppearInLeaf()
    {
        await _tree.Insert(10, 1, 1);

        var leaf = await _pageManager.LoadPage<LeafPage>(_rootPageId);

        Assert.Equal(1, leaf.Header.KeyCount);
        Assert.Equal(10, leaf.Keys[0]);
    }
    
    [Fact]
    public async Task Insert_MultipleKeys_ShouldBeSorted()
    {
        await _tree.Insert(30, 1, 1);
        await _tree.Insert(10, 1, 1);
        await _tree.Insert(20, 1, 1);

        var leaf = await _pageManager.LoadPage<LeafPage>(_rootPageId);

        Assert.Equal(3, leaf.Header.KeyCount);
        Assert.Equal([10, 20, 30], leaf.Keys.Take(3));
    }
    
    [Fact]
    public async Task Insert_ShouldSplitLeaf_WhenOverflow()
    {
        for (int i = 0; i < LeafPage.MaxKeys; i++)
            await _tree.Insert(i, 1, 1);
        await _tree.Insert(999, 1, 1);

        var keys = await _tree.InOrder();
        var expected = Enumerable.Range(0, LeafPage.MaxKeys).Append(999).ToArray();
        
        Assert.Equal(expected, keys);
    }
    
    [Fact]
    public async Task Insert_ManyKeys_ShouldPreserveAllValues()
    {
        int count = LeafPage.MaxKeys * 100;

        for (int i = 0; i < count; i++)
            await _tree.Insert(i, 1, 1);

        var result = await _tree.InOrder();

        Assert.Equal(count, result.Length);
        Assert.Equal(Enumerable.Range(0, count), result);
    }
    
    [Fact]
    public async Task Delete_SingleKey_ShouldRemoveIt()
    {
        for (int i = 0; i < 20; i++)
            await _tree.Insert(i, 1, 1);

        await _tree.Delete(10);

        var result = await _tree.InOrder();

        Assert.DoesNotContain(10, result);
        Assert.Equal(19, result.Length);
    }
    
    [Fact]
    public async Task Delete_ForcesIndexMerge_ShouldRemainConsistent()
    {
        int keyCount = LeafPage.MaxKeys * IndexPage.MaxKeys * 3;
    
        for (int i = 0; i < keyCount * 2; i++)
            await _tree.Insert(i, 1, 1);

        for (int i = 2 * keyCount - 1; i >= keyCount; i--)
            await _tree.Delete(i);

        var keys = await _tree.InOrder();
        var expected = Enumerable.Range(0, keyCount).ToArray();
        Assert.Equal(expected.Length, keys.Length);
        Assert.Equal(expected, keys);
    }
    
    [Fact]
    public async Task RandomDeleteAllTest()
    {
        var rnd = new Random();

        int count = 10000;

        var keys = Enumerable.Range(1, count).ToList();

        foreach (var k in keys)
            await _tree.Insert(k, 1, 1);

        keys = keys.OrderBy(_ => rnd.Next()).ToList();

        foreach (var k in keys)
        {
            await _tree.Delete(k);

            var treeInOrder = await _tree.InOrder();

            for (int i = 1; i < treeInOrder.Length; i++)
                Assert.True(treeInOrder[i - 1] < treeInOrder[i]);

            Assert.DoesNotContain(k, treeInOrder);
        }

        var final = await _tree.InOrder();
        Assert.Empty(final);
    }
    
    [Fact]
    public async Task Delete_AllKeys_ShouldResultInEmptyTree()
    {
        int count = 32 * (LeafPage.MaxKeys * 3 + 338);

        for (int i = 0; i < count; i++)
            await _tree.Insert(i, 1, 1);

        for (int i = 0; i < count; i++)
            await _tree.Delete(i);

        var result = await _tree.InOrder();

        Assert.Empty(result);
    }
    
    [Fact]
    public async Task Insert_Delete_Insert_ShouldRemainCorrect()
    {
        for (int i = 0; i < 500000; i++)
            await _tree.Insert(i, 1, 1);

        for (int i = 250000; i < 500000; i++)
            await _tree.Delete(i);

        for (int i = 250000; i < 500000; i++)
            await _tree.Insert(i, 1, 1);

        var result = await _tree.InOrder();

        Assert.Equal(Enumerable.Range(0, 500000), result);
    }
    
    [Fact]
    public async Task Concurrent_Insert_ShouldNotCorruptTree()
    {
        int threadCount = 32;
        int keysPerThread = 5000;

        ConcurrentBag<int> expected = new ConcurrentBag<int>();

        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(async () =>
            {
                for (int i = 0; i < keysPerThread; i++)
                {
                    int key = t * keysPerThread + i;
                    expected.Add(key);
                    await _tree.Insert(key, 1, 1);
                }
            }))
            .ToArray();
        
        await Task.WhenAll(tasks);

        var keys = await _tree.InOrder();
        Assert.Equal(threadCount * keysPerThread, keys.Length);
        Assert.Equal(expected.OrderBy(x => x), keys);
    }
    
    [Fact]
    public async Task Concurrent_Delete_ShouldNotCorruptTree()
    {
        int threadCount = 32;
        int keysPerThread = 50000;
        var keyCount = threadCount * keysPerThread * 2;
        
        for (int i = 0; i < keyCount; i++)
            await _tree.Insert(i, 1, 1); ;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(async () =>
            {
                for (int i = 0; i < keysPerThread; i++)
                {
                    int key = t * keysPerThread + i;
                    await _tree.Delete(key);
                }
            }))
            .ToArray();
        
        await Task.WhenAll(tasks);

        var keys = await _tree.InOrder();
        var expected = Enumerable.Range(keyCount / 2, keyCount / 2);
        Assert.Equal(threadCount * keysPerThread, keys.Length);
        Assert.Equal(expected.OrderBy(x => x), keys);
    }
    
    [Fact]
    public async Task Concurrent_InsertAndDelete_ShouldNotCorruptTree()
    {
        int threadCount = 32;
        int keysPerThread = LeafPage.MaxKeys * 200;
        int totalKeys = threadCount * keysPerThread;
        
        for (int i = totalKeys / 2; i < totalKeys; i++)
            await _tree.Insert(i, 1, 1);

        var insertTasks = Enumerable.Range(0, threadCount / 2)
            .Select(t => Task.Run(async () =>
            {
                for (int i = 0; i < keysPerThread; i++)
                {
                    int key = t * keysPerThread + i;
                    try
                    {
                        await _tree.Insert(key, 1, 1);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }));

        var deleteTasks = Enumerable.Range(threadCount / 2, threadCount / 2)
            .Select(t => Task.Run(async () =>
            {
                for (int i = 0; i < keysPerThread; i++)
                {
                    int key = t * keysPerThread + i;
                    try
                    {
                        await _tree.Delete(key);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }));

        await Task.WhenAll(insertTasks.Concat(deleteTasks));

        var keys = await _tree.InOrder();
        
        var expected = Enumerable.Range(0, totalKeys / 2).OrderBy(x => x);
        Assert.Equal(expected, keys);
    }
}