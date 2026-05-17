using AzotBase.Database.Controllers;
using Microsoft.Extensions.Configuration;

namespace AzotBase.Database.Api;

public class AzotBase
{
    private readonly DbDiskManager _dbDiskManager;
    
    public AzotBase(IConfiguration config)
    {
        _dbDiskManager = new DbDiskManager(config);
    }
    
    public DBTable<T> ConnectTable<T>(string tableName)
    {
        if (!_dbDiskManager.FindTable(tableName))
            throw new Exception("Table " + tableName + " does not exists");
        
        return new DBTable<T>(Path.Combine(_dbDiskManager.TablesPath, tableName), tableName);
    }

    public void CreateTable<T>(string tableName, bool autoConnect = false)
    {
        if (_dbDiskManager.FindTable(tableName))
            throw new Exception("Table " + tableName + " already exists");
        
        _dbDiskManager.CreateTable<T>(tableName);
    }
    
    public void DeleteTable(string tableName)
    {
        if (!_dbDiskManager.FindTable(tableName))
            throw new Exception("Table " + tableName + " does not exist");
        
        _dbDiskManager.DeleteTable(tableName);
    }
}