using System.Diagnostics.CodeAnalysis;
using AzotBase.Page;
using Microsoft.Extensions.Configuration;

namespace AzotBase;

public class DbDiskManager
{
    public string DbPath = string.Empty;
    public string TablesPath = string.Empty;

    public DbDiskManager(IConfiguration config)
    {
        CreateDBDirectory(config);
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

    public void CreateTable<T>(string tableName)
    {
        if (FindTable(tableName))
            throw new Exception("Table " + tableName + " already exists");

        File.Create(Path.Combine(TablesPath, tableName, $"{tableName}.adb")).Dispose();
        File.Create(Path.Combine(TablesPath, "lowLoad"));
        File.Create(Path.Combine(TablesPath, "highLoad"));
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
        foreach (var path in Directory.EnumerateDirectories(TablesPath, tableName))
        {
            foreach (var file in Directory.EnumerateFiles(path, filePattern))
            {
                if (Path.GetFileName(file) == tableName)
                    return true;
            }
        }
        return false;
    }
}