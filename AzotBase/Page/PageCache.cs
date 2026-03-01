using System.Diagnostics.CodeAnalysis;

namespace AzotBase.Page;

public class PageCache<V> where V : IPage
{
    public class Node
    {
        public int Key;
        public V Value;
        
        public Node Next;
        public Node Prev;

        public int PinCount;

        public Node(int key, V value)
        {
            Key = key;
            Value = value;
        }
    }
    
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
    
    private readonly Dictionary<int, Node> _cache = new Dictionary<int, Node>();
    private readonly Node head;
    private Node tail;
    private int _pinnedCount;
    private readonly SemaphoreSlim _unpinnedPageSignal = new SemaphoreSlim(0);

    public PageCache(int capacity)
    {
        head = tail = new Node(default, default);
        Capacity = capacity;
    }

    private Lock _lruLock = new Lock();
    public bool TryGetValue(int key, [NotNullWhen(true)] out V? value)
    {
        lock(_lruLock)
        {
            if (!_cache.TryGetValue(key, out Node? cachedNode))
            {
                value = default(V);
                return false;
            }
            
            if (cachedNode != tail)
            {
                DeleteNode(cachedNode);
                InsertTail(cachedNode);
            }
            
            value = cachedNode.Value;
            return true;
        }
    }

    private SemaphoreSlim _addLock = new SemaphoreSlim(1, 1);
    public async Task AddAsync(int key, V value)
    {
        var node = new Node(key, value);
        if (_cache.TryGetValue(key, out Node? cachedNode))
        {
            if (cachedNode != tail)
            {
                DeleteNode(cachedNode);
                InsertTail(node);
                _cache[key] = node;
            }
            cachedNode.Value = value;
            return;
        }

        Node? deletedNode = null;
        //If all nodes pinned then cash blocked
        await _addLock.WaitAsync();

        if (_cache.Count == Capacity)
        {
            while (!TryDeleteFirstUnpinned(out deletedNode))
                await _unpinnedPageSignal.WaitAsync();
        }
        InsertTail(node);
        _cache[key] = node;
        
        _addLock.Release();
        
        if (deletedNode != null)
            OnDeleteEvent(new DeleteEventArgs(deletedNode.Key, deletedNode.Value));
    }
    
    public void PinPage(int pageId)
    {
        if (!_cache.TryGetValue(pageId, out var node))
            throw new Exception("Cache doesn't contain this pageId");
        
        var result = Interlocked.Increment(ref node.PinCount);
        if (result == 1)
            Interlocked.Increment(ref _pinnedCount);
    }

    public void UnpinPage(int pageId)
    {
        if (!_cache.TryGetValue(pageId, out var node))
            throw new Exception("Cache doesn't contain this pageId");
        
        var result = Interlocked.Decrement(ref node.PinCount);
        if (result == 0)
        {
            Interlocked.Decrement(ref _pinnedCount);
            _unpinnedPageSignal.Release();
        }
    }
    
    private void InsertTail(Node node)
    {
        node.Prev = tail;
        tail.Next = node;
        tail = tail.Next;
    }

    private void DeleteNode(Node node)
    {
        node.Prev.Next = node.Next;
        if (node.Next != null)
            node.Next.Prev = node.Prev;
    }

    private bool TryDeleteFirstUnpinned([NotNullWhen(true)] out Node? deletedNode)
    {
        if (_pinnedCount == Capacity)
        {
            deletedNode = null;
            return false;
        }
        
        var curr = head.Next;
        while (curr != null && curr.PinCount != 0)
            curr = curr.Next;

        if (curr == null)
        {
            deletedNode = null;
            return false;
        }

        var node = _cache[curr.Key];
        DeleteNode(node);
        _cache.Remove(curr.Key);

        deletedNode = node;
        return true;
    }
    
    private void OnDeleteEvent(DeleteEventArgs e) => DeleteEvent?.Invoke(this, e);
}