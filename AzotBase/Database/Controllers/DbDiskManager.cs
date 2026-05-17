using Microsoft.Extensions.Configuration;

namespace AzotBase.Database.Controllers;

public class DbDiskManager
{
    public string DbPath = string.Empty;
    public string TablesPath = string.Empty;

    public DbDiskManager(IConfiguration config)
    {
        CreateDBDirectory(config);
    }
    
    public void CreateTable<T>(string tableName)
    {
        if (FindTable(tableName))
            throw new Exception("Table " + tableName + " already exists");

        File.Create(Path.Combine(TablesPath, tableName, $"{tableName}.adb")).Dispose();
    }
    
    public void DeleteTable(string tableName)
    {
        if (!FindTable(tableName))
            throw new Exception("Table " + tableName + " does not exist");
        
        Directory.Delete(Path.Combine(TablesPath, tableName), true);
    }
    
    public bool FindTable(string tableName)
    {
        var filePattern = $"*{tableName}.adb";
        
        return Directory
            .EnumerateDirectories(TablesPath, tableName)
            .SelectMany(path => Directory.EnumerateFiles(path, filePattern))
            .Any(file => Path.GetFileName(file) == tableName);
    }
    
    private void CreateDBDirectory(IConfiguration config)
    {
        var path = config.GetSection("DBSettings").GetSection("DBDirectoryPath").Value 
                   ?? throw new Exception("DBDirectoryPath not set");
        var dbName = config.GetSection("DBSettings").GetSection("DBName").Value ?? "AzotBase" + Guid.NewGuid();
        
        DbPath = Path.Combine(path, dbName);
        TablesPath = Path.Combine(DbPath, "tables");
        var dbDirectory = Directory.CreateDirectory(DbPath);
        dbDirectory.CreateSubdirectory(TablesPath);
    }
}