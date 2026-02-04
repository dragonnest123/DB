using AzotBase.Page;

namespace Test.BPlusTreeTest;

public class IndexPageTest
{
    [Fact]
    public void IndexPage_InsertKey_ShouldAddKeyAndChildren()
    {
        var index = new IndexPage(1);
        index.InsertKey(10, 100, 101);
        index.InsertKey(5, 102, 103);
        index.InsertKey(20, 104, 105);

        Assert.Equal(3, index.Header.KeyCount);
        Assert.Equal([5, 10, 20], index.Keys[..3]);
        Assert.Equal([102, 103, 104, 105], index.ChildrenPageIds[..4]);
    }

    [Fact]
    public void IndexPage_DeleteKey_ShouldRemoveKeyAndShiftChildren()
    {
        var index = new IndexPage(1);
        index.InsertKey(10, 100, 101);
        index.InsertKey(5, 102, 103);
        index.InsertKey(20, 104, 105);

        index.DeleteKey(10);

        Assert.Equal(2, index.Header.KeyCount);
        Assert.Equal([5, 20], index.Keys[..2]);
        Assert.Equal([102, 103, 105], index.ChildrenPageIds[..3]);
    }

    [Fact]
    public void IndexPage_FindKey_ShouldReturnCorrectIndex()
    {
        var index = new IndexPage(1);
        index.InsertKey(10, 0, 0);
        index.InsertKey(20, 0, 0);
        index.InsertKey(30, 0, 0);

        Assert.Equal(0, index.FindKey(10));
        Assert.Equal(1, index.FindKey(20));
        Assert.Equal(2, index.FindKey(30));
        Assert.Equal(-1, index.FindKey(40));
    }

    [Fact]
    public void IndexPage_Serialization_ShouldPreserveData()
    {
        var index = new IndexPage(1);
        index.InsertKey(10, 100, 101);
        index.InsertKey(20, 102, 103);

        var bytes = index.ToByteArray();
        var deserialized = IndexPage.FromByteArray(bytes);

        Assert.Equal(index.Header.KeyCount, deserialized.Header.KeyCount);
        Assert.Equal(index.Keys[..index.Header.KeyCount], deserialized.Keys[..deserialized.Header.KeyCount]);
        for (int i = 0; i <= index.Header.KeyCount; i++)
            Assert.Equal(index.ChildrenPageIds[i], deserialized.ChildrenPageIds[i]);
    }
}