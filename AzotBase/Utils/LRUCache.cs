using System.Diagnostics.CodeAnalysis;

namespace AzotBase.Utils;

public class LRUCache<T,V> 
    where T : IComparable<T> 
    where V : notnull
{
    public class Node
    {
        public T Key;
        public V Value;
        public Node Next;
        public Node Prev;

        public Node(T key, V value)
        {
            Key = key;
            Value = value;
        }
    }
    
    private readonly Dictionary<T, Node> cache = new Dictionary<T, Node>();
    private readonly Node head;
    private Node tail;
    private int Capacity;

    public LRUCache(int capacity)
    {
        head = tail = new Node(default, default);
        Capacity = capacity;
    }

    public bool TryGetValue(T key, [NotNullWhen(true)] out V? value)
    {
        if (!cache.TryGetValue(key, out Node? cachedNode))
        {
            value = default;
            return false;
        }

        if (cache[key] == tail)
        {
            value = cachedNode.Value;
            return true;
        }
       
        var node = new Node(key, cachedNode.Value);
        DeleteNode(cachedNode);
        InsertTail(node);
        cache[key] = node;
        
        value = cachedNode.Value;
        return true;
    }

    public void Add(T key, V value)
    {
        var node = new Node(key, value);
        if (cache.TryGetValue(key, out Node? cachedNode))
        {
            if (cachedNode != tail)
            {
                DeleteNode(cachedNode);
                InsertTail(node);
                cache[key] = node;
            }
            cachedNode.Value = value; 
        }
        else
        {
            InsertTail(node);
            cache[key] = node;
            if (cache.Count > Capacity)
            {
                var leastUsed = head.Next.Key;
                DeleteNode(cache[leastUsed]);
                cache.Remove(leastUsed);
            }
        }
    }
    
    private void DeleteNode(Node node)
    {
        node.Prev.Next = node.Next;
        node.Next.Prev = node.Prev;
    }

    private void InsertTail(Node node)
    {
        node.Prev = tail;
        tail.Next = node;
        tail = tail.Next;
    }
}