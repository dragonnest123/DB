using System.Runtime.InteropServices;

namespace AzotBase.Page.Header;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SystemPageHeader : IPageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<SystemPageHeader>(); //RAM
    
    public int Id { get; set; }
    public PageType PageType { get; set; }
}