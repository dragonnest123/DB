namespace AzotBase.Page.PageCache;

public class CacheNode<V>
{
    public readonly int Key;
    public V Value;
        
    public CacheNode<V>? Next;
    public CacheNode<V>? Prev;

    public int PinCount;
            
    public LinkedListNode<CacheNode<V>>? UnpinnedRef;

    public CacheNode(int key, V value)
    {
        Key = key;
        Value = value;
    }
}