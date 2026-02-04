using System.Runtime.InteropServices;

namespace AzotBase.Page.Header;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LeafPageHeader : ITreePageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<LeafPageHeader>(); //RAM
    
    public int Id { get; set; }
    public PageType PageType { get; set; }
    public int ParentId { get; set; }
    public int KeyCount { get; set; }
    public byte IsLeaf { get; set; }
    public int NextLeafPageId { get; set; }
    
    public LeafPageHeader(int id)
    {
        Id = id;
        ParentId = -1;
        NextLeafPageId = -1;
    }
}
