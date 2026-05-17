namespace AzotBase.Common.Collections.Lru;

public class CacheList<K, V>
{
    private readonly CacheNode<K, V> _head;
    private CacheNode<K, V> _tail;

    public CacheList()
    {
        _head = _tail = new CacheNode<K, V>(default!, default!);
    }
        
    public void InsertTail(CacheNode<K, V> node)
    {
        node.Prev = _tail;
        _tail.Next = node;
        _tail = node;
    }

    public void DeleteNode(CacheNode<K, V> node)
    {
        node.Prev!.Next = node.Next;

        if (node.Next != null)
            node.Next.Prev = node.Prev;
        else
            _tail = node.Prev;

        node.Next = null;
        node.Prev = null;
    }
    
    public void MoveToTail(CacheNode<K, V> node)
    {
        if (node == _tail) 
            return;
        
        DeleteNode(node);
        InsertTail(node);
    }
}