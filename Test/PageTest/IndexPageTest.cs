using AzotBase.Page;

namespace Test.PageTest;

public class IndexPageTests
{
    [Fact]
    public void CreateEmpty_ShouldInitializeWithNoKeys_AndChildrenSetToMinusOne()
    {
        var page = IndexPage.CreateEmpty(1);

        Assert.Equal(0, page.Header.KeyCount);

        for (int i = 0; i < IndexPage.MaxKeys + 1; i++)
            Assert.Equal(-1, page.ChildrenPageIds[i]);
    }

    [Fact]
    public void InsertKey_ShouldInsertInSortedOrder()
    {
        var page = IndexPage.CreateEmpty(1);

        page.ChildrenPageIds[0] = 10;

        page.InsertKey(20, 30);
        page.InsertKey(10, 20);
        page.InsertKey(15, 25);

        Assert.Equal(3, page.Header.KeyCount);

        Assert.Equal(10, page.Keys[0]);
        Assert.Equal(15, page.Keys[1]);
        Assert.Equal(20, page.Keys[2]);

        Assert.Equal(10, page.ChildrenPageIds[0]);
        Assert.Equal(20, page.ChildrenPageIds[1]);
        Assert.Equal(25, page.ChildrenPageIds[2]);
        Assert.Equal(30, page.ChildrenPageIds[3]);
    }

    [Fact]
    public void InsertKeyAt_ShouldInsertAtSpecifiedIndex()
    {
        var page = IndexPage.CreateEmpty(1);
        page.ChildrenPageIds[0] = 1;

        page.InsertKey(10, 2);
        page.InsertKey(30, 4);

        page.InsertKeyAt(1, 20, 3);

        Assert.Equal(3, page.Header.KeyCount);

        Assert.Equal([10, 20, 30], page.Keys[..3]);

        Assert.Equal(1, page.ChildrenPageIds[0]);
        Assert.Equal(2, page.ChildrenPageIds[1]);
        Assert.Equal(3, page.ChildrenPageIds[2]);
        Assert.Equal(4, page.ChildrenPageIds[3]);
    }
    
    

    [Fact]
    public void DeleteKey_ShouldRemoveKey_AndShiftLeft()
    {
        var page = IndexPage.CreateEmpty(1);
        page.ChildrenPageIds[0] = 1;

        page.InsertKey(10, 2);
        page.InsertKey(20, 3);
        page.InsertKey(30, 4);

        page.DeleteKey(20);

        Assert.Equal(2, page.Header.KeyCount);

        Assert.Equal(10, page.Keys[0]);
        Assert.Equal(30, page.Keys[1]);

        Assert.Equal(1, page.ChildrenPageIds[0]);
        Assert.Equal(2, page.ChildrenPageIds[1]);
        Assert.Equal(4, page.ChildrenPageIds[2]);
    }

    [Fact]
    public void DeleteKeyAt_ShouldRemoveKeyByIndex()
    {
        var page = IndexPage.CreateEmpty(1);
        page.ChildrenPageIds[0] = 1;

        page.InsertKey(10, 2);
        page.InsertKey(20, 3);
        page.InsertKey(30, 4);

        page.DeleteKeyAt(0);

        Assert.Equal(2, page.Header.KeyCount);

        Assert.Equal(20, page.Keys[0]);
        Assert.Equal(30, page.Keys[1]);

        Assert.Equal(1, page.ChildrenPageIds[0]);
        Assert.Equal(3, page.ChildrenPageIds[1]);
        Assert.Equal(4, page.ChildrenPageIds[2]);
    }
    
    [Fact]
    public void DeleteKey_ShouldDoNothing_WhenKeyNotFound()
    {
        var page = IndexPage.CreateEmpty(1);
        page.ChildrenPageIds[0] = 1;

        page.InsertKey(10, 2);

        page.DeleteKey(999);

        Assert.Equal(1, page.Header.KeyCount);
        Assert.Equal(10, page.Keys[0]);
    }

    [Fact]
    public void ToByteArray_And_FromByteArray_ShouldPreserveData()
    {
        var page = IndexPage.CreateEmpty(42);
        page.ChildrenPageIds[0] = 100;

        page.InsertKey(10, 200);
        page.InsertKey(20, 300);

        var bytes = page.ToByteArray();
        var restored = IndexPage.FromByteArray(bytes);

        Assert.Equal(page.Header.Id, restored.Header.Id);
        Assert.Equal(page.Header.KeyCount, restored.Header.KeyCount);

        for (int i = 0; i < page.Header.KeyCount; i++)
            Assert.Equal(page.Keys[i], restored.Keys[i]);

        for (int i = 0; i < page.Header.KeyCount + 1; i++)
            Assert.Equal(page.ChildrenPageIds[i], restored.ChildrenPageIds[i]);
    }
}