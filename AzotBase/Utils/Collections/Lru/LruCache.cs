using System.Diagnostics.CodeAnalysis;

namespace AzotBase.Utils.Collections.Lru;

// ReSharper disable InconsistentlySynchronizedField
public class LruCache<K, V> 
    where K : notnull
    where V : notnull
{
    public readonly int Capacity;
    public event Action<K, V>? OnEvicted;
    
    private readonly Dictionary<K, CacheNode<K, V>> _cache = new Dictionary<K, CacheNode<K, V>>();
    private readonly CacheList<K, V> _cacheList = new CacheList<K, V>();
    private readonly LinkedList<CacheNode<K, V>> _unpinnedList = [];

    private readonly SemaphoreSlim _unpinnedSignal = new SemaphoreSlim(0); //в теории может быть переполнение
    private readonly Lock _lock = new Lock();

    public LruCache(int capacity)
    {
        Capacity = capacity;
    }
    
    public bool TryGetValue(K key, [NotNullWhen(true)] out V? value)
    {
        lock (_lock)
        {
            if (!TryGetNodeUnsafe(key, out var node))
            {
                value = default;
                return false;
            }
            
            value = node.Value;
            return true;
        }
    }
    
    public async Task<bool> TryAddAsync(K key, V value)
    {
        if (_cache.ContainsKey(key))
            return false;
        
        var node = new CacheNode<K, V>(key, value);
        CacheNode<K, V>? deletedNode = null;
        
        while (true)
        {
            _lock.Enter();
    
            if (_cache.Count < Capacity || TryDeleteFirstUnpinnedUnsafe(out deletedNode))
                break;
    
            _lock.Exit();
            await _unpinnedSignal.WaitAsync(100);
        }
        
        if (_cache.ContainsKey(key))
        {
            _lock.Exit();
            
            if (deletedNode != null)
                OnEvictedEvent(deletedNode.Key, deletedNode.Value);
            return false;
        }
        
        node.UnpinnedRef = _unpinnedList.AddLast(node);
        _cacheList.InsertTail(node);
        _cache[node.Key] = node;
        
        _lock.Exit();
        
        if (deletedNode != null)
            OnEvictedEvent(deletedNode.Key, deletedNode.Value);
        
        return true;
    }
    
    public V? Pin(K key)
    {
        CacheNode<K,V>? node;
        lock (_lock)
        {
            if (!TryGetNodeUnsafe(key, out node)) 
                return default;
            
            if (node.UnpinnedRef != null)
            {
                _unpinnedList.Remove(node.UnpinnedRef);
                node.UnpinnedRef = null;
            }
            
            node.PinCount++;
        }

        return node.Value;
    }
    
    public void Unpin(K key)
    {
        if (!_cache.TryGetValue(key, out var node))
            throw new Exception("Cache doesn't contain this key");

        lock (_lock)
        {
            if (node.PinCount - 1 < 0)
                throw new Exception("This key already unpinned");

            if (--node.PinCount == 0)
            {
                node.UnpinnedRef = _unpinnedList.AddLast(node);
                if (_cache.Count == Capacity)
                    _unpinnedSignal.Release();
            }
        }
    }
    
    private bool TryDeleteFirstUnpinnedUnsafe([NotNullWhen(true)] out CacheNode<K, V>? deletedNode)
    {
        var deletedTail = _unpinnedList.First;
        if (deletedTail == null)
        {
            deletedNode = null;
            return false;
        }
        
        _unpinnedList.RemoveFirst();
        deletedTail.Value.UnpinnedRef = null;
        
        _cache.Remove(deletedTail.Value.Key, out _);
        _cacheList.DeleteNode(deletedTail.Value);
        
        deletedNode = deletedTail.Value;
        return true;
    }

    private bool TryGetNodeUnsafe(K key, [NotNullWhen(true)] out CacheNode<K, V>? node)
    {
        if (!_cache.TryGetValue(key, out node))
            return false;
        
        _cacheList.MoveToTail(node);
        if (node.UnpinnedRef != null)
        {
            _unpinnedList.Remove(node.UnpinnedRef);
            node.UnpinnedRef = _unpinnedList.AddLast(node);
        }
        
        return true;
    }
    
    private void OnEvictedEvent(K key, V value) => OnEvicted?.Invoke(key, value);
}
