using AzotBase.Database.Controllers;

namespace AzotBase.Database.Api;

public class DBTable<T>
{
    private TableDiskManager _dbDiskManager;
    
    public DBTable(string tableDirectoryPath, string tableName)
    {
        _dbDiskManager = new TableDiskManager(tableDirectoryPath, tableName);
    }

    public T? Find(int id)
    {
        throw new NotImplementedException();        
    }

    public async ValueTask<T> FindAsync(int id)
    {
        throw new NotImplementedException();
    }

    public bool Delete(int id)
    {
        throw new NotImplementedException();
    }

    public async ValueTask<bool> DeleteAsync(int id)
    {
        throw new NotImplementedException();
    }

    public void CreateRecord(T record)
    {
        
    }

    public async ValueTask CreateRecordAsync(T record)
    {
        throw new NotImplementedException();
    }

    public void UpdateRecord(int id, T newRecord)
    {
        throw new NotImplementedException();
    }

    public async ValueTask UpdateRecordAsync(int id, T newRecord)
    {
        throw new NotImplementedException();
    }
}