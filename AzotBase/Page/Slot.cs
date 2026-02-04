using System.Runtime.InteropServices;

namespace AzotBase.Page;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Slot
{
    public const ushort SlotSize = 12;
    public int Offset; //DISK
    public int Length; //DISK
    public int NextPageId; //DISK

    public Slot(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }
}