namespace AzotBase.Utils;

public class AsyncReaderWriterLock
{
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly Lazy<SemaphoreSlim> _upgradeLock = new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(0));
    
    private readonly SemaphoreSlim _readerQueue = new SemaphoreSlim(0);
    private readonly SemaphoreSlim _writerQueue = new SemaphoreSlim(0);

    private int _readerCount;
    private int _waitingWritersCount;
    private int _waitingReadersCount;
    private bool _writerActive;
    private bool _upgradeActive;

    public async Task EnterReadLock(int milliSecondsTimeout = Timeout.Infinite)
    {
        await _lock.WaitAsync(milliSecondsTimeout);

        if (_writerActive || _waitingWritersCount > 0)
        {
            _waitingReadersCount++;
            
            _lock.Release();
            await _readerQueue.WaitAsync(milliSecondsTimeout);
        }
        else
        {
            _readerCount++;
            _lock.Release();
        }
    }

    public async Task<bool> TryUpgradeReadLock(int milliSecondsTimeout = Timeout.Infinite)
    {
        await _lock.WaitAsync(milliSecondsTimeout);
        
        if (_upgradeActive || _writerActive)
        {
            _lock.Release();
            return false;
        }

        _readerCount--;
        if (_readerCount > 0)
        {
            _waitingWritersCount++;
            _upgradeActive = true;
            
            _lock.Release();
            await _upgradeLock.Value.WaitAsync(milliSecondsTimeout);
            return true;
        }

        if (_readerCount < 0)
            throw new InvalidOperationException("Upgrade operation with 0 readers");
      
        _writerActive = true;
        
        _lock.Release();
        return true;
    }

    public async Task ExitReadLock(int milliSecondsTimeout = Timeout.Infinite)
    {
        await _lock.WaitAsync();

        _readerCount--;
        if (_readerCount > 0)
        {
            _lock.Release();
            return;
        }

        if (_readerCount < 0)
        {
            _lock.Release();
            throw new InvalidOperationException("Exit operation with 0 readers");
        }

        if (_upgradeActive)
        {
            _waitingWritersCount--;
            _writerActive = true;
            _upgradeActive = false;
            _upgradeLock.Value.Release();
        }
        else if (_waitingWritersCount > 0)
        {
            _waitingWritersCount--;
            _writerActive = true;
            _writerQueue.Release();
        }
        
        _lock.Release();
    }

    public async Task EnterWriteLock(int milliSecondsTimeout = Timeout.Infinite)
    {
        await _lock.WaitAsync(milliSecondsTimeout);

        if (_writerActive || _upgradeActive || _readerCount > 0)
        {
            _waitingWritersCount++;
            
            _lock.Release();
            await _writerQueue.WaitAsync();
        }
        else
        {
            _writerActive = true;
            _lock.Release();
        }
    }

    public async Task ExitWriteLock(int milliSecondsTimeout = Timeout.Infinite)
    {
        await _lock.WaitAsync();

        if (_waitingWritersCount > 0)
        {
            _waitingWritersCount--;
            _writerQueue.Release();
        }
        else if (_waitingReadersCount > 0)
        {
            var count = _waitingReadersCount;
            _readerCount += count;
            _waitingReadersCount = 0;
            _writerActive = false;
            _readerQueue.Release(count);
        }
        else if (_writerActive)
            _writerActive = false;
        else
            throw new InvalidOperationException("Exit operation with 0 writers");
        
        _lock.Release();
    }
}