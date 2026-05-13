using AzotBase.Utils.Collections.Lru;

namespace Test.Utils.Collections.Lru;

public class CacheListTest
{
    private CacheNode<int, int> Node(int key) => new CacheNode<int, int>(key, key);

    [Fact]
    public void InsertTail_ShouldAppendNodes()
    {
        var list = new CacheList<int, int>();
        var n1 = Node(1);
        var n2 = Node(2);

        list.InsertTail(n1);
        list.InsertTail(n2);

        Assert.Equal(n1, n2.Prev);
        Assert.Equal(n2, n1.Next);
    }

    [Fact]
    public void DeleteNode_Middle_ShouldRelinkNeighbors()
    {
        var list = new CacheList<int, int>();
        var n1 = Node(1);
        var n2 = Node(2);
        var n3 = Node(3);

        list.InsertTail(n1);
        list.InsertTail(n2);
        list.InsertTail(n3);

        list.DeleteNode(n2);

        Assert.Equal(n3, n1.Next);
        Assert.Equal(n1, n3.Prev);
        Assert.Null(n2.Next);
        Assert.Null(n2.Prev);
    }

    [Fact]
    public void DeleteNode_Tail_ShouldUpdateTail()
    {
        var list = new CacheList<int, int>();
        var n1 = Node(1);
        var n2 = Node(2);

        list.InsertTail(n1);
        list.InsertTail(n2);

        list.DeleteNode(n2);

        Assert.Null(n1.Next);
        Assert.Null(n2.Prev);
    }

    [Fact]
    public void MoveToTail_ShouldReorderNodes()
    {
        var list = new CacheList<int, int>();
        var n1 = Node(1);
        var n2 = Node(2);
        var n3 = Node(3);

        list.InsertTail(n1);
        list.InsertTail(n2);
        list.InsertTail(n3);

        list.MoveToTail(n1);

        Assert.Equal(n3, n2.Next);
        Assert.Equal(n1, n3.Next);
        Assert.Null(n1.Next);
    }

    [Fact]
    public void MoveToTail_AlreadyTail_ShouldDoNothing()
    {
        var list = new CacheList<int, int>();
        var n1 = Node(1);
        var n2 = Node(2);

        list.InsertTail(n1);
        list.InsertTail(n2);

        list.MoveToTail(n2);

        Assert.Equal(n2, n1.Next);
        Assert.Null(n2.Next);
    }
}