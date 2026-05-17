using System.Runtime.InteropServices;
using AzotBase.Storage.Pages.Common;

namespace AzotBase.Storage.Pages.Headers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LeafPageHeader : ITreePageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<LeafPageHeader>(); 
    
    public int Id { get; set; }
    public PageType PageType { get; set; }
    public int KeyCount { get; set; }
    public int NextLeafPageId { get; set; }
    
    public LeafPageHeader(int id)
    {
        Id = id;
        PageType = PageType.LeafPage;
        NextLeafPageId = -1;
    }
}
