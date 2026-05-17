using AzotBase.Storage.Pages.Common;
using AzotBase.Storage.Pages.Headers;

namespace AzotBase.Storage.Pages;

public class SystemPage : PageBase, IPage<SystemPage>
{
    public const ushort PageSize = 4096;
    
    public override int Id => Header.Id;
    public SystemPageHeader Header { get; set; }
    public Queue<int> FreePages = new Queue<int>();

    public override byte[] ToByteArray()
    {
        throw new NotImplementedException();
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