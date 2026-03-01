using AzotBase.Page;

namespace Test;

public class PageManagerFactory
{
    public static PageManager Create()
    {
        var path = Path.GetTempFileName();

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.DeleteOnClose);

        return new PageManager(stream);
    }
}