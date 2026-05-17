using System.Runtime.InteropServices;
using AzotBase.Storage.Pages.Common;

namespace AzotBase.Storage.Pages.Headers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SystemPageHeader : IPageHeader
{
    public static readonly ushort LengthBytes = (ushort)Marshal.SizeOf<SystemPageHeader>();
    
    public int Id { get; set; }
    public PageType PageType { get; set; }
}