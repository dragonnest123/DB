using AzotBase.Page.Header;
using AzotBase.Utils;

namespace AzotBase.Page;

public class DataPage : IPage<DataPage>
{
    private DataPageHeader _header; //DISK
    private readonly byte[] _data; //DISK

    public DataPage(int id)
    {
        _header = new DataPageHeader(id);    
        _data = new byte[SystemPage.PageSize - DataPageHeader.LengthBytes];
    }

    private DataPage(DataPageHeader header, byte[] data)
    {
        _header = header;
        _data = data;
    }

    public ReadOnlySpan<byte> GetRecord(int slotIndex)
    {
        var firstByteSlot = slotIndex * Slot.SlotSize;
        var slot = StructSerializer.Deserialize<Slot>(_data.AsSpan()[firstByteSlot..(firstByteSlot + Slot.SlotSize)]);

        return _data.AsSpan()[slot.Offset..(slot.Offset + slot.Length)];
    }

    public int WriteRecord(byte[] record)
    {
        var freeSpaceLength = _header.FreeSpaceEnd - _header.FreeSpaceStart + 1;
        
        if (Slot.SlotSize >= freeSpaceLength)
            return 0;
        
        int recordPos = Math.Max(_header.FreeSpaceStart + Slot.SlotSize, _header.FreeSpaceEnd - record.Length + 1);
        int byteToWrite = Math.Min(freeSpaceLength - Slot.SlotSize, record.Length);
        
        WriteSlot(recordPos, byteToWrite);
        
        Array.Copy(record, 0, _data, recordPos, byteToWrite);
        _header.FreeSpaceEnd = (ushort)(recordPos - 1);
        _header.RecordCount++;
        
        return byteToWrite;
    }

    public void DeleteRecord(int slotIndex)
    {   
        var firstByteSlot = slotIndex * Slot.SlotSize;
        var slot = StructSerializer.Deserialize<Slot>(_data.AsSpan()[firstByteSlot..(firstByteSlot + Slot.SlotSize)]);
        WriteSlot(slot.Offset, 0);
    }   
    
    public byte[] ToByteArray()
    {
        var result = new byte[SystemPage.PageSize];

        StructSerializer.Serialize(result, 0, ref _header);
        Array.Copy(_data, 0, result, DataPageHeader.LengthBytes, _data.Length);
        
        return result;
    }
    
    public static DataPage FromByteArray(Span<byte> bytes)
    {
        var header = StructSerializer.Deserialize<DataPageHeader>(bytes[..DataPageHeader.LengthBytes]);
        
        return new DataPage(header, bytes[DataPageHeader.LengthBytes..].ToArray());
    }

    private void WriteSlot(int recordPos, int byteToWrite)
    {
        var slot = new Slot(recordPos, byteToWrite);
        var slotBytes = StructSerializer.Serialize(ref slot);
        
        Array.Copy(slotBytes, 0, _data, _header.FreeSpaceStart, Slot.SlotSize);
        _header.FreeSpaceStart += Slot.SlotSize;
    }
}