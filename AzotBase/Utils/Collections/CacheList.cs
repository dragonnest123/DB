using AzotBase.Page;

namespace AzotBase.Utils.Collections;

public class CacheList<V> where V : PageBase
{
    private readonly CacheNode<V> _head;
    private CacheNode<V> _tail;
        
    private readonly Lock _nodeLock = new Lock();

    public CacheList()
    {
        _head = _tail = new CacheNode<V>(0, null!);
    }
        
    public void InsertTail(CacheNode<V> node)
    {
        lock (_nodeLock)
        {
            InsertTailInternal(node);
        }
    }

    public void DeleteNode(CacheNode<V> node)
    {
        lock (_nodeLock)
        {
            DeleteNodeInternal(node);
        }
    }
    
    public void MoveToTail(CacheNode<V> node)
    {
        if (node == _tail) 
            return;

        lock (_nodeLock)
        {
            DeleteNodeInternal(node);
            InsertTailInternal(node);
        }
    }
    
    private void InsertTailInternal(CacheNode<V> node)
    {
        node.Prev = _tail;
        _tail.Next = node;
        _tail = node;
    }

    private void DeleteNodeInternal(CacheNode<V> node)
    {
        node.Prev!.Next = node.Next;

        if (node.Next != null)
            node.Next.Prev = node.Prev;
        else
            _tail = node.Prev;

        node.Next = null;
        node.Prev = null;
    }
}