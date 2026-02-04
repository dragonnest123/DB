using System.Runtime.InteropServices;

namespace AzotBase.Page.Header;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DataPageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<DataPageHeader>(); //RAM
    
    public int Id; 
    public PageType PageType;
    
    public ushort RecordCount; 
    public ushort FreeSpaceStart; 
    public ushort FreeSpaceEnd;
    
    public DataPageHeader(int id)
    {
        Id = id;
        PageType = PageType.DataPage;
        FreeSpaceStart = 0;
        FreeSpaceEnd = (ushort)(SystemPage.PageSize - LengthBytes - 1);
    }
}