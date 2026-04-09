namespace AzotBase.Utils.LockUtils;

public class ReaderWriterLockAdapter : IReaderWriterLock
{
    private readonly ReaderWriterLockSlim _readerWriterLock = new ReaderWriterLockSlim();
    
    public ValueTask EnterReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        if (_readerWriterLock.TryEnterReadLock(millisecondsTimeout))
            throw new TimeoutException("EnterReadLock timed out");
        
        return ValueTask.CompletedTask;
    }

    public ValueTask ExitReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        _readerWriterLock.ExitReadLock();
        
        return ValueTask.CompletedTask;
    }

    public ValueTask EnterWriteLock(int millisecondsTimeout = Timeout.Infinite)
    {
        if (!_readerWriterLock.TryEnterWriteLock(millisecondsTimeout))
            throw new TimeoutException("EnterWriteLock timed out");
        
        return ValueTask.CompletedTask;
    }

    public ValueTask ExitWriteLock(int millisecondsTimeout = Timeout.Infinite)
    {
        _readerWriterLock.ExitWriteLock();
        
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryUpgradeReadLock(int millisecondsTimeout = Timeout.Infinite)
    {
        throw new NotImplementedException();
    }
}