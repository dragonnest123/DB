using System.Collections.Concurrent;
using AzotBase.Page;
using AzotBase.Tree;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Test.BPlusTreeTest;

public class TreeTest
{
    private readonly PageManager _pageManager;
    private readonly BPlusTree _tree;
    private readonly int _rootPageId;

    private ITestOutputHelper _output = new TestOutputHelper();

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
        int count = LeafPage.MaxKeys * 20;

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
    public async Task Delete_ManyKeys_ShouldRemainConsistent()
    {
        int count = LeafPage.MaxKeys * 16;
        
        for (int i = 0; i < count; i++)
            await _tree.Insert(i, 1, 1);
        
        for (int i = 0; i < count; i += 2)
            await _tree.Delete(i);

        var result = await _tree.InOrder();
        var expected = Enumerable.Range(0, count)
            .Where(x => x % 2 != 0);

        Assert.Equal(expected, result);
    }
    
    [Fact]
    public async Task Delete_AllKeys_ShouldResultInEmptyTree()
    {
        int count = LeafPage.MaxKeys * 3;

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
        for (int i = 0; i < 50; i++)
            await _tree.Insert(i, 1, 1);

        for (int i = 0; i < 50; i++)
            await _tree.Delete(i);

        for (int i = 100; i < 150; i++)
            await _tree.Insert(i, 1, 1);

        var result = await _tree.InOrder();

        Assert.Equal(Enumerable.Range(100, 50), result);
    }
    
    [Fact]
    public async Task Concurrent_Insert_ShouldNotCorruptTree()
    {
        int threadCount = 16;
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
    public async Task Insert_SameKeys_HighContention_ShouldBeCorrect()
    {
        int threadCount = 8;
        var barrier = new Barrier(threadCount);
    
        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < 100; i++)
                    await _tree.Insert(t + i * threadCount, 1, 1);
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        var result = await _tree.InOrder();
        Assert.Equal(threadCount * 100, result.Length);
    }
}