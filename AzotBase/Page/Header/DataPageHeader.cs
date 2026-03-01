using System.Runtime.InteropServices;

namespace AzotBase.Page.Header;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DataPageHeader : IPageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<DataPageHeader>(); //RAM
    
    public int Id { get; set; }
    public PageType PageType { get; set; }
    public ushort RecordCount { get; set; } 
    public ushort FreeSpaceStart { get; set; }
    public ushort FreeSpaceEnd { get; set; }

    public DataPageHeader(int id)
    {
        Id = id;
        PageType = PageType.DataPage;
        FreeSpaceStart = 0;
        FreeSpaceEnd = (ushort)(SystemPage.PageSize - LengthBytes - 1);
    }
}