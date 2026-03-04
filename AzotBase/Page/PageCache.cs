using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace AzotBase.Page;

public class PageCache<V> where V : PageBase
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
    
    private readonly ConcurrentDictionary<int, Node> _cache = new ConcurrentDictionary<int, Node>();
    private readonly Node head;
    private Node tail;
    private int _pinnedCount;
    
    private readonly SemaphoreSlim _unpinnedPageSignal = new SemaphoreSlim(0);
    private readonly SemaphoreSlim _addLock = new SemaphoreSlim(1, 1);
    private readonly Lock _lock = new Lock();

    public PageCache(int capacity)
    {
        head = tail = new Node(default, default);
        Capacity = capacity;
    }
    public bool TryGetValue(int key, [NotNullWhen(true)] out V? value)
    {
        if (_cache.TryGetValue(key, out Node? cachedNode))
        {
            MoveToTail(cachedNode);
            value = cachedNode.Value;
            return true;
        }
        
        value = default(V);
        return false;
    }
    
    public async Task AddAsync(int key, V value)
    {
        if (_cache.TryGetValue(key, out Node? cachedNode))
        {
            cachedNode.Value = value;
            MoveToTail(cachedNode);
            return;
        }
        
        var node = new Node(key, value);
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
        lock (_lock)
        {
            node.Prev = tail;
            tail.Next = node;
            tail = tail.Next;
        }
    }

    private void DeleteNode(Node node)
    {
        lock (_lock)
        {
            node.Prev.Next = node.Next;
            if (node.Next != null)
                node.Next.Prev = node.Prev;
        }
    }
    
    private void MoveToTail(Node node)
    {
        if (node == tail) 
            return;

        lock (_lock)
        {
            DeleteNode(node);
            InsertTail(node);
        }
    }

    private bool TryDeleteFirstUnpinned([NotNullWhen(true)] out Node? deletedNode)
    {
        if (_pinnedCount == Capacity)
        {
            deletedNode = null;
            return false;
        }

        Node? curr;
        lock (_lock)
        {
            curr = head.Next;
            while (curr != null && curr.PinCount != 0)
                curr = curr.Next;

            if (curr == null)
            {
                deletedNode = null;
                return false;
            }

            DeleteNode(curr);
        }
        
        if (_cache.TryRemove(curr.Key, out _))
        {
            deletedNode = curr;
            return true;
        }

        deletedNode = null;
        return false;
    }
    
    private void OnDeleteEvent(DeleteEventArgs e) => DeleteEvent?.Invoke(this, e);
}