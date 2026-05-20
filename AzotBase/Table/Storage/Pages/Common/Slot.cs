using System.Runtime.InteropServices;

namespace AzotBase.Storage.Pages.Common;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Slot
{
    public static readonly ushort SlotSize = (ushort)Marshal.SizeOf<Slot>();
    public int Offset; //DISK
    public int Length; //DISK
    public int NextPageId; //DISK

    public Slot(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }
}