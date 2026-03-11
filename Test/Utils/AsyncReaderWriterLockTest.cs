using AzotBase.Utils;

namespace Test.Utils;

public class AsyncReaderWriterLockTest
{
    private readonly AsyncReaderWriterLock _rwLock = new AsyncReaderWriterLock();
    
    [Fact]
    public async Task MultipleReaders_CanEnterSimultaneously()
    {
        int concurrentReaders = 0;
        int maxConcurrent = 0;
    
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _rwLock.EnterReadLock();
            
                var current = Interlocked.Increment(ref concurrentReaders);
                if (current > maxConcurrent) maxConcurrent = current;

                await Task.Delay(50); 

                Interlocked.Decrement(ref concurrentReaders);
                
                await _rwLock.ExitReadLock();
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(5, maxConcurrent);
    }
    
    [Fact]
    public async Task RandomReadersAndWriters_DoNotViolateInvariants_FixedIterations()
    {
        int activeReaders = 0;
        int activeWriters = 0;
        int violationDetected = 0;

        var tasks = Enumerable.Range(0, 40).Select(_ => Task.Run(async () =>
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode());

            for (int i = 0; i < 5; i++)
            {
                bool isWriter = rnd.Next(0, 3) == 0;
                
                if (isWriter)
                {
                    await _rwLock.EnterWriteLock();

                    var writers = Interlocked.Increment(ref activeWriters);
                    if (writers > 1)
                        Interlocked.Exchange(ref violationDetected, 1);

                    if (Volatile.Read(ref activeReaders) > 0)
                        Interlocked.Exchange(ref violationDetected, 1);
                    
                    Interlocked.Decrement(ref activeWriters);
                    
                    await _rwLock.ExitWriteLock();
                }
                else
                {
                    await _rwLock.EnterReadLock();

                    Interlocked.Increment(ref activeReaders);

                    if (Volatile.Read(ref activeWriters) > 0)
                        Interlocked.Exchange(ref violationDetected, 1);

                    Interlocked.Decrement(ref activeReaders);
                    await _rwLock.ExitReadLock();
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(0, violationDetected);
    }
    
    [Fact]
    public async Task Upgrade_DoesNotViolateInvariants()
    {
        int activeReaders = 0;
        int activeWriters = 0;
        int violationDetected = 0;

        var tasks = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode());

            for (int i = 0; i < 50000; i++)
            {
                bool isWriter = rnd.Next(0, 1) == 0;     
                bool doUpgrade = !isWriter && rnd.Next(0, 1) == 0; 

                if (isWriter)
                {
                    await _rwLock.EnterWriteLock();
                    var writers = Interlocked.Increment(ref activeWriters);
                    if (writers > 1 || Volatile.Read(ref activeReaders) > 0)
                        Interlocked.Exchange(ref violationDetected, 1);
                    
                    Interlocked.Decrement(ref activeWriters);

                    await _rwLock.ExitWriteLock();
                }
                else
                {
                    await _rwLock.EnterReadLock();
                    Interlocked.Increment(ref activeReaders);

                    if (Volatile.Read(ref activeWriters) > 0)
                        Interlocked.Exchange(ref violationDetected, 1);

                    if (doUpgrade)
                    {
                        Interlocked.Decrement(ref activeReaders);
                        
                        if (!await _rwLock.TryUpgradeReadLock())
                        {
                            await _rwLock.ExitReadLock();
                            continue;
                        }
                        
                        var writers = Interlocked.Increment(ref activeWriters);
                        if (writers > 1 || Volatile.Read(ref activeReaders) > 0)
                            Interlocked.Exchange(ref violationDetected, 1);

                        Interlocked.Decrement(ref activeWriters);
                        await _rwLock.ExitWriteLock();
                    }
                    else
                    {
                        Interlocked.Decrement(ref activeReaders);
                        await _rwLock.ExitReadLock();
                    }
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(0, violationDetected);
    }
}