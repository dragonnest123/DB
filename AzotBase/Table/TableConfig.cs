namespace AzotBase.Database;

public class TableConfig
{
    public string TableName { get; set; } = Guid.CreateVersion7().ToString();

    public string DataFilePath { get; init; }
    public string FsmFilePath { get; init; }
    
    private TableConfig()
    {
        DataFilePath = Path.Combine(DbDirectoryPath, DBName + ".db");
        FsmFilePath = Path.Combine(DbDirectoryPath, DBName + ".fsm");
    }
}