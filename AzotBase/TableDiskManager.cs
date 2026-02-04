using AzotBase.Page;
using AzotBase.Tree;

namespace AzotBase;

public class TableDiskManager : IDisposable, IAsyncDisposable
{
    private readonly FileStream _freePageStreamLowLoad;
    private readonly FileStream _freePageStreamHighLoad;
    private readonly FileStream _dataStream;
    private readonly PageManager _pageManager;
    private readonly BPlusTree _treeCache;
    private readonly SystemPage _systemPage;

    public TableDiskManager(string tableDirectoryPath, string tableName)
    {
        _freePageStreamLowLoad = File.Open(Path.Combine(tableDirectoryPath, "lowLoad"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        _freePageStreamHighLoad = File.Open(Path.Combine(tableDirectoryPath, "highLoad"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        _dataStream = File.Open(Path.Combine(tableDirectoryPath, $"{tableName}.adb"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        _systemPage = ReadSystemPage();
    }

    public byte[] GetRecord(int recordId)
    {
        throw new NotImplementedException();
        //systempage 
    }

    public void Dispose()
    {
        _freePageStreamLowLoad.Dispose();
        _freePageStreamHighLoad.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _freePageStreamLowLoad.DisposeAsync();
        await _freePageStreamHighLoad.DisposeAsync();
    }
    
    private async Task WriteSystemPage()
    {
        var bytes = _systemPage.ToByteArray();
        await _dataStream.WriteAsync(bytes);
    }

    private SystemPage ReadSystemPage()
    {
        var bytes = new byte[SystemPage.PageSize];
        _dataStream.ReadExactly(bytes);
        return SystemPage.FromByteArray(bytes);
    }
    
}