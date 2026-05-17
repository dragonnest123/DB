namespace AzotBase.Common.Collections.Lru;

public class CacheNode<K, V>
{
    public readonly K Key;
    public readonly V Value; 

    public CacheNode<K, V>? Next;
    public CacheNode<K, V>? Prev;
    public LinkedListNode<CacheNode<K, V>>? UnpinnedRef;

    public int PinCount;

    public CacheNode(K key, V value)
    {
        Key = key;
        Value = value;
    }
}