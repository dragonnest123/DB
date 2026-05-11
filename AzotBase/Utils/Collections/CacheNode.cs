namespace AzotBase.Utils.Collections;

public class CacheNode<K, V>
{
    public readonly K Key;
    public V Value;
        
    public CacheNode<K, V>? Next;
    public CacheNode<K, V>? Prev;

    public int PinCount;
            
    public CacheNode(K key, V value)
    {
        Key = key;
        Value = value;
    }
}