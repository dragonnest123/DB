using System.Diagnostics.CodeAnalysis;

namespace AzotBase.Page.PageCache;

public class PageCache<V> where V : PageBase
{
    public class DeleteEventArgs : EventArgs
    {
        public int Key { get; set; }
        public V Value { get; set; }

        public DeleteEventArgs(int key, V value)
        {
            Key = key;
            Value = value;
        }
    }
    
    public readonly int Capacity;
    public event EventHandler<DeleteEventArgs>? DeleteEvent;
    
    private readonly Dictionary<int, CacheNode<V>> _cache = new Dictionary<int, CacheNode<V>>();
    private readonly CacheList<V> _cacheList = new CacheList<V>();
    private readonly LinkedList<CacheNode<V>> _unpinnedList = new LinkedList<CacheNode<V>>();
    
    private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
    private readonly Lock _pinLock = new Lock();
    private readonly SemaphoreSlim _unpinnedSignal = new SemaphoreSlim(0);

    public PageCache(int capacity)
    {
        Capacity = capacity;
    }
    
    public bool TryGetValue(int key, [NotNullWhen(true)] out V? value, bool makePin = false)
    {
        _rwLock.EnterReadLock();
        
        if (!_cache.TryGetValue(key, out var node))
        {
            _rwLock.ExitReadLock();
            
            value = null;
            return false;
        }

        if (makePin)
        {
            lock (_pinLock)
            {
                PinPage(node);
            }
        }
        
        _cacheList.MoveToTail(node);
        
        if (node.UnpinnedRef != null)
        {
            _unpinnedList.Remove(node.UnpinnedRef);
            node.UnpinnedRef = _unpinnedList.AddLast(node);
        }
        
        _rwLock.ExitReadLock();
        
        value = node.Value;
        return true;
    }
    
    public async Task<bool> TryAddAsync(int key, V value, bool makePin = false, bool overrideIfExist = true)
    {
        _rwLock.EnterUpgradeableReadLock();
        
        if (_cache.TryGetValue(key, out var cached))
        {
            _cacheList.MoveToTail(cached);

            if (makePin)
                PinPage(cached);
            
            if (!overrideIfExist)
            {
                _rwLock.ExitUpgradeableReadLock();
                return false;
            }
            
            cached.Value = value;
            _rwLock.ExitUpgradeableReadLock();
            return true;
        }
        
        _rwLock.EnterWriteLock();
        
        var node = new CacheNode<V>(key, value);
        CacheNode<V>? deletedNode = null;
        
        while (_cache.Count == Capacity)
        {
            if (TryDeleteFirstUnpinned(out deletedNode)) 
                break;
            
            _rwLock.ExitWriteLock();
            _rwLock.ExitUpgradeableReadLock();

            await _unpinnedSignal.WaitAsync(100);
                
            _rwLock.EnterUpgradeableReadLock();
            _rwLock.EnterWriteLock();
        }
        
        _cacheList.InsertTail(node);
        _cache[node.Key] = node;
        
        if (makePin)
            PinPage(node);
        else 
            node.UnpinnedRef = _unpinnedList.AddLast(node);
        
        _rwLock.ExitWriteLock();
        _rwLock.ExitUpgradeableReadLock();
        
        if (deletedNode != null)
            OnDeleteEvent(new DeleteEventArgs(deletedNode.Key, deletedNode.Value));
        
        return true;
    }
    
    public void UnpinPage(int pageId)
    {
        _rwLock.EnterUpgradeableReadLock();

        if (!_cache.TryGetValue(pageId, out var node))
        {
            _rwLock.ExitUpgradeableReadLock();
            throw new Exception("Cache doesn't contain this pageId");
        }

        if (Interlocked.Decrement(ref node.PinCount) != 0)
        {
            _rwLock.ExitUpgradeableReadLock();
            return;
        }
        
        _rwLock.EnterWriteLock();
        
        node.UnpinnedRef = _unpinnedList.AddLast(node);
        
        _unpinnedSignal.Release();
        
        _rwLock.ExitWriteLock();
        _rwLock.ExitUpgradeableReadLock();
        
    }

    public V[] ToArray() => _cache.Values.Select(x => x.Value).ToArray();
    
    public List<V> ToList() => _cache.Values.Select(x => x.Value).ToList();
    
    private void PinPage(CacheNode<V> node)
    {
        if (Interlocked.Increment(ref node.PinCount) != 1)
            return;

        var refNode = node.UnpinnedRef;
        if (refNode != null)
        {
            _unpinnedList.Remove(refNode);
            node.UnpinnedRef = null;
        }
    }

    private bool TryDeleteFirstUnpinned([NotNullWhen(true)] out CacheNode<V>? deletedNode)
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
    
    private void OnDeleteEvent(DeleteEventArgs e) => DeleteEvent?.Invoke(this, e);
}