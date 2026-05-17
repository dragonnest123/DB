using AzotBase.Index;
using AzotBase.Storage;
using AzotBase.Storage.Pages;

namespace AzotBase.Database.Controllers;

public class TableDiskManager : IDisposable, IAsyncDisposable
{
    private readonly FileStream _dataStream;
    private readonly PageManager _pageManager;
    private readonly BPlusTree _indexes;
    private readonly SystemPage _systemPage;

    public TableDiskManager(string tableDirectoryPath, string tableName)
    {
        _dataStream = File.Open(
            Path.Combine(tableDirectoryPath, $"{tableName}.adb"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        _pageManager = new PageManager(_dataStream);
        _indexes = new BPlusTree(_pageManager);
        
        _systemPage = ReadSystemPage();
    }

    public byte[] GetRecord(int recordId)
    {
        throw new NotImplementedException();
        //systempage 
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

    public void Dispose()
    {
        _dataStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _dataStream.DisposeAsync();
    }
}