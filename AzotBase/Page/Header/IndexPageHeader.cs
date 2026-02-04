using System.Runtime.InteropServices;

namespace AzotBase.Page.Header;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IndexPageHeader : ITreePageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<IndexPageHeader>(); //RAM
    
    public int Id { get; set; }
    public PageType PageType { get; set; }
    public int ParentId { get; set; }
    public int KeyCount { get; set; }
    public byte IsLeaf { get; set; }
    
    public IndexPageHeader(int id)
    {
        Id = id;
        ParentId = -1;
    }
}