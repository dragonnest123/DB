using System.Runtime.InteropServices;
using AzotBase.Storage.Pages.Common;

namespace AzotBase.Storage.Pages.Headers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IndexPageHeader : ITreePageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<IndexPageHeader>(); 
    
    public int Id { get; set; }
    public PageType PageType { get; set; }
    public int KeyCount { get; set; }
    
    public IndexPageHeader(int id)
    {
        Id = id;
        PageType = PageType.IndexPage;
    }
}