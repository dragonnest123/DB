using AzotBase.Page.Header;
using AzotBase.Utils;

namespace AzotBase.Page;

public class DataPage : PageBase, IPage<DataPage>
{
    public DataPageHeader Header; //DISK
    public override int Id => Header.Id;
    
    private readonly byte[] _data; //DISK

    public DataPage(int id)
    {
        Header = new DataPageHeader(id);    
        _data = new byte[SystemPage.PageSize - DataPageHeader.LengthBytes];
    }

    private DataPage(DataPageHeader header, byte[] data)
    {
        Header = header;
        _data = data;
    }

    public ReadOnlySpan<byte> GetRecord(int slotIndex)
    {
        EnterReadLock();
        
        var firstByteSlot = slotIndex * Slot.SlotSize;
        var slot = StructSerializer.Deserialize<Slot>(_data.AsSpan()[firstByteSlot..(firstByteSlot + Slot.SlotSize)]);
        var record = _data.AsSpan()[slot.Offset..(slot.Offset + slot.Length)];
        
        ExitReadLock();

        return record;
    }

    public (int slotID, int writtenBytes) WriteRecord(byte[] record)
    {
        EnterWriteLock(); 
        
        var freeSpaceLength = Header.FreeSpaceEnd - Header.FreeSpaceStart + 1;
        
        if (Slot.SlotSize >= freeSpaceLength)
            return (-1, 0);
        
        int slotId = Header.FreeSpaceStart / Slot.SlotSize;
        int recordPos = Math.Max(Header.FreeSpaceStart + Slot.SlotSize, Header.FreeSpaceEnd - record.Length + 1);
        int byteToWrite = Math.Min(freeSpaceLength - Slot.SlotSize, record.Length);
        
        WriteSlot(recordPos, byteToWrite);
        
        Array.Copy(record, 0, _data, recordPos, byteToWrite);
        Header.FreeSpaceEnd = (ushort)(recordPos - 1);
        Header.RecordCount++;
        IsDirty = 1;
        
        ExitWriteLock();
        
        return (slotId, byteToWrite);
    }

    public void DeleteRecord(int slotIndex)
    {   
        EnterWriteLock();
        
        var firstByteSlot = slotIndex * Slot.SlotSize;
        var slot = StructSerializer.Deserialize<Slot>(_data.AsSpan()[firstByteSlot..(firstByteSlot + Slot.SlotSize)]);
        WriteSlot(slot.Offset, 0);
        
        ExitWriteLock();
    }

    public override byte[] ToByteArray()
    {
        EnterReadLock();
        
        var result = new byte[SystemPage.PageSize];

        StructSerializer.Serialize(result, 0, ref Header);
        Array.Copy(_data, 0, result, DataPageHeader.LengthBytes, _data.Length);
        
        ExitReadLock();
        
        return result;
    }

    public new static DataPage FromByteArray(Span<byte> bytes)
    {
        var header = StructSerializer.Deserialize<DataPageHeader>(bytes[..DataPageHeader.LengthBytes]);
        if (header.PageType != PageType.DataPage)
            throw new Exception("Invalid page type");
        
        return new DataPage(header, bytes[DataPageHeader.LengthBytes..].ToArray());
    }

    public static DataPage CreateEmpty(int id) => new DataPage(id);

    private void WriteSlot(int recordPos, int byteToWrite)
    {
        var slot = new Slot(recordPos, byteToWrite);
        var slotBytes = StructSerializer.Serialize(ref slot);
        
        Array.Copy(slotBytes, 0, _data, Header.FreeSpaceStart, Slot.SlotSize);
        Header.FreeSpaceStart += Slot.SlotSize;
    }
}