using System.Collections.Concurrent;
using System.Diagnostics;

namespace AzotBase.Utils;

public static class LockTracker
{
    private static readonly ConcurrentDictionary<int, int> _waitingFor = [];
    private static readonly ConcurrentDictionary<int, int> _heldBy = [];  
    private static int CurrentId => Task.CurrentId ?? Environment.CurrentManagedThreadId;

    public static void WaitingFor(int pageId) => _waitingFor[CurrentId] = pageId;

    public static void Acquired(int pageId)
    {
        _heldBy[pageId] = CurrentId;
        _waitingFor.TryRemove(CurrentId, out _);
    }

    public static void Released(int pageId) => _heldBy.TryRemove(pageId, out _);
    
    public static void CancelWait() => _waitingFor.TryRemove(CurrentId, out _);

    public static void Dump()
    {
        foreach (var (thread, page) in _waitingFor)
        {
            _heldBy.TryGetValue(page, out var holder);
            if (holder != 0) 
                Debug.WriteLine($"Thread {thread} waits page {page}, held by thread {holder}");
        }
    }
}