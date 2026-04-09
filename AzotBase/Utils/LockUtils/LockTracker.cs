using System.Collections.Concurrent;
using System.Diagnostics;

namespace AzotBase.Utils.LockUtils;

public static class LockTracker
{
    public enum LockOperation
    {
        Read,
        Write,
        Upgrade
    }
    
    private struct LockInfo
    {
        public int ThreadId;
        public LockOperation LockOp;
    }
    
    private static readonly ConcurrentDictionary<LockInfo, int> _waitingFor = [];
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, LockInfo>> _heldBy = [];
    private static readonly List<string> _dumped = [];
    
    private static int _currentId => Task.CurrentId ?? Environment.CurrentManagedThreadId;
    private static readonly Lock _dumpLock = new Lock();

    public static void WaitingFor(int pageId, LockOperation op)
    {
        var info = new LockInfo { ThreadId = _currentId, LockOp = op };
        _waitingFor[info] = pageId;   
    }

    public static void Acquired(int pageId, LockOperation op)
    {
        var info = new LockInfo { ThreadId = _currentId, LockOp = op };
        
        var holder = _heldBy.GetOrAdd(pageId, _ => new ConcurrentDictionary<int, LockInfo>());
        holder[_currentId] = info;
        
        _waitingFor.TryRemove(info, out _);
    }

    public static void Released(int pageId)
    {
        if (!_heldBy.TryGetValue(pageId, out var holder)) 
            return;
        
        holder.TryRemove(_currentId, out _);
        
        if (holder.IsEmpty)
            _heldBy.TryRemove(pageId, out _);
    }
        
    public static void CancelWait(int threadId, LockOperation op)
    {
        var info = new LockInfo { ThreadId = threadId, LockOp = op };
        _waitingFor.TryRemove(info, out _);
    }
    
    public static void ClearTask()
    {
        var id = _currentId;
        foreach (var (info, _) in _waitingFor.Where(kv => kv.Key.ThreadId == id).ToList())
            _waitingFor.TryRemove(info, out _);
        foreach (var (_, holders) in _heldBy)
            holders.TryRemove(id, out _);
    }

    public static void Dump(string methodName = "")
    {
        lock (_dumpLock)
        {
            if (_dumped.Contains(methodName))
                return;
            
            Debug.WriteLine($"==== DUMP {methodName} ====");
            foreach (var (lockInfo, pageId) in _waitingFor)
            {
                _heldBy.TryGetValue(pageId, out var holders);
                var holderStr = holders != null 
                    ? string.Join(",", holders.Select(h => $"Task{h.Key}"))
                    : "none";
                Debug.WriteLine($"Task {lockInfo.ThreadId} waits page {pageId} with {lockInfo.LockOp}, holders=[{holderStr}]");
            }

            _dumped.Add(methodName);
        }
    }
}