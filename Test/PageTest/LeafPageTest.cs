using AzotBase.Index;
using AzotBase.Page;

namespace Test.PageTest;

public class LeafPageTest
{
    [Fact]
    public void LeafPage_InsertKey_ShouldAddKeyAndValue()
    {
        var leaf = new LeafPage(1);
        leaf.InsertKey(10, 100, 1);
        leaf.InsertKey(5, 101, 2);
        leaf.InsertKey(20, 102, 3);

        Assert.Equal(3, leaf.Header.KeyCount);
        Assert.Equal([5, 10, 20], leaf.Keys[..3]);
        Assert.Equal((101, 2), leaf.Values[0]);
        Assert.Equal((100, 1), leaf.Values[1]);
        Assert.Equal((102, 3), leaf.Values[2]);
    }

    [Fact]
    public void LeafPage_DeleteKey_ShouldRemoveKeyAndValue()
    {
        var leaf = new LeafPage(1);
        leaf.InsertKey(10, 100, 1);
        leaf.InsertKey(5, 101, 2);
        leaf.InsertKey(20, 102, 3);

        leaf.DeleteKey(10);

        Assert.Equal(2, leaf.Header.KeyCount);
        Assert.Equal([5, 20], leaf.Keys[..2]);
        Assert.Equal((101, 2), leaf.Values[0]);
        Assert.Equal((102, 3), leaf.Values[1]);
    }

    [Fact]
    public void LeafPage_FindKey_ShouldReturnCorrectIndex()
    {
        var leaf = new LeafPage(1);
        leaf.InsertKey(10, 0, 0);
        leaf.InsertKey(20, 0, 0);
        leaf.InsertKey(30, 0, 0);

        Assert.Equal(0, leaf.FindKey(10));
        Assert.Equal(1, leaf.FindKey(20));
        Assert.Equal(2, leaf.FindKey(30));
        Assert.Equal(-1, leaf.FindKey(40));
    }

    [Fact]
    public void LeafPage_Serialization_ShouldPreserveData()
    {
        var leaf = new LeafPage(1);
        leaf.InsertKey(10, 100, 1);
        leaf.InsertKey(20, 200, 2);

        var bytes = leaf.ToByteArray();
        var deserialized = LeafPage.FromByteArray(bytes);

        Assert.Equal(leaf.Header.KeyCount, deserialized.Header.KeyCount);
        Assert.Equal(leaf.Keys[..leaf.Header.KeyCount], deserialized.Keys[..deserialized.Header.KeyCount]);
        for (int i = 0; i < leaf.Header.KeyCount; i++)
            Assert.Equal(leaf.Values[i], deserialized.Values[i]);
    }
}