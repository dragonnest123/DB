namespace AzotBase.Common.Concurrency;

public interface IReaderWriterLock
{
    public ValueTask EnterReadLock(int millisecondsTimeout = Timeout.Infinite);
    
    public ValueTask ExitReadLock(int millisecondsTimeout = Timeout.Infinite);
    
    public ValueTask EnterWriteLock(int millisecondsTimeout = Timeout.Infinite);
    
    public ValueTask ExitWriteLock(int millisecondsTimeout = Timeout.Infinite);
    
    public ValueTask<bool> TryUpgradeReadLock(int millisecondsTimeout = Timeout.Infinite);
}