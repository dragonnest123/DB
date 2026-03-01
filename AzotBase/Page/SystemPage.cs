using System.Runtime.CompilerServices;

namespace AzotBase.Page;

public class SystemPage : PageBase, IPage<SystemPage>
{
    public const ushort PageSize = 4096;
    public int PagesCount; //DISK
    public int NextPageIndex = -1; //DISK
    public Queue<int> FreePages = new Queue<int>(); //RAM

    public override byte[] ToByteArray()
    {
        var array = new byte[PageSize];
        
        Array.Copy(BitConverter.GetBytes(PagesCount), 0, array, 0, sizeof(int));
        Array.Copy(BitConverter.GetBytes(NextPageIndex), 0, array, sizeof(int), sizeof(int));
        
        int currByte = 2 * sizeof(int);
        foreach (var pageIndex in FreePages)
        {
            Array.Copy(BitConverter.GetBytes(pageIndex), 0, array, currByte, sizeof(int));
            currByte += sizeof(int);
        }
        
        return array;
    }

    public static SystemPage FromByteArray(byte[] data)
    {
        const int intSize = sizeof(int);
        
        var systemPage = new SystemPage();
        systemPage.PagesCount = BitConverter.ToInt32(data, intSize);
        systemPage.NextPageIndex = BitConverter.ToInt32(data, intSize);
        var currByte = 2 * intSize;
    
        while (currByte + intSize < data.Length)
        {
            systemPage.FreePages.Enqueue(BitConverter.ToInt32(data, currByte));
            currByte += intSize;
        }

        return systemPage;
    }

    public static SystemPage FromByteArray(Span<byte> bytes)
    {
        throw new NotImplementedException();
    }

    public static SystemPage CreateEmpty(int id)
    {
        throw new NotImplementedException();
    }
}