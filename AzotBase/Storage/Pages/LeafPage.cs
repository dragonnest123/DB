using System.Runtime.InteropServices;
using AzotBase.Common.Serialization;
using AzotBase.Storage.Pages.Common;
using AzotBase.Storage.Pages.Headers;

namespace AzotBase.Storage.Pages;

public class LeafPage : BPlusTreePage<LeafPageHeader>, IPage<LeafPage>
{
    public static readonly int MaxKeys = (SystemPage.PageSize - LeafPageHeader.LengthBytes - sizeof(int)) / (3 * sizeof(int));
    
    public override int Id => Header.Id;
    public readonly (int PageId, int SlotId)[] Values; //DISK
    
    public LeafPage(int id) : base(new LeafPageHeader(id), MaxKeys)
    {
        Values = new (int PageId, int SlotId)[MaxKeys];
    }
    
    private LeafPage(LeafPageHeader header, int[] keys,  (int PageId, int SlotId)[] values) : base(header, keys)
    {
        Values = values;
    }
    
    public void InsertKey(int key, int pageId, int slotId)
    {
        int i = Header.KeyCount - 1;
        while (i >= 0 && Keys[i] > key)
        {
            Keys[i + 1] = Keys[i];
            Values[i + 1] = Values[i];
            i--;
        }
        
        Keys[i + 1] = key;
        Values[i + 1] = (pageId, slotId);
        
        Header.KeyCount++;
        IsDirty = 1;
        
        for (int j = 1; j < Header.KeyCount; j++)
            if (Keys[j] <= Keys[j-1])
                throw new Exception($"LeafPage {Header.Id} unsorted after InsertKey({key}) at {j}: {Keys[j-1]} >= {Keys[j]}");
    }
    
    public void InsertKeyAt(int index, int key, int pageId, int slotId)
    {
        int keysToMove = Header.KeyCount - index;
        
        Array.Copy(Keys, index, Keys, index + 1, keysToMove);
        Keys[index] = key;
        
        Array.Copy(Values, index, Values, index + 1, keysToMove);
        Values[index] = (pageId, slotId);
        
        Header.KeyCount++;
        IsDirty = 1;
        
        for (int j = 1; j < Header.KeyCount; j++)
            if (Keys[j] <= Keys[j-1])
                throw new Exception($"LeafPage {Header.Id} unsorted after InsertKeyAt({key}) at {j}: {Keys[j-1]} >= {Keys[j]}");
    }

    public void InsertRangeAt(int index, Span<int> keys, Span<(int PageId, int SlotId)> values)
    {
        keys.CopyTo(Keys.AsSpan()[index..]);
        values.CopyTo(Values.AsSpan()[index..]);
        
        Header.KeyCount += keys.Length;
        IsDirty = 1;
        
        for (int j = 1; j < Header.KeyCount; j++)
            if (Keys[j] <= Keys[j-1])
                throw new Exception($"LeafPage {Header.Id} unsorted after InsertRangeAt at {j}: {Keys[j-1]} >= {Keys[j]}");
    }

    public void DeleteKey(int key)
    {
        int index = FindKey(key);
        if (index < 0) 
            return;
        
        int keysToMove = Header.KeyCount - (index + 1);
        if (keysToMove > 0)
        {
            Array.Copy(Keys, index + 1, Keys, index, keysToMove);
            Array.Copy(Values, index + 1, Values, index, keysToMove);
        }

        Header.KeyCount--;
        IsDirty = 1;
        
        for (int j = 1; j < Header.KeyCount; j++)
            if (Keys[j] <= Keys[j-1])
                throw new Exception($"LeafPage {Header.Id} unsorted after DeleteKey({key}) at {j}: {Keys[j-1]} >= {Keys[j]}");
    }
    
    public void DeleteKeyAt(int index)
    {
        int keysToMove = Header.KeyCount - (index + 1);
        
        Array.Copy(Keys, index + 1, Keys, index, keysToMove);
        Array.Copy(Values, index + 1, Values, index, keysToMove);
        
        Header.KeyCount--;
        IsDirty = 1;
        
        for (int j = 1; j < Header.KeyCount; j++)
            if (Keys[j] <= Keys[j-1])
                throw new Exception($"LeafPage {Header.Id} unsorted after InsertKeyAt at {j}: {Keys[j-1]} >= {Keys[j]}");
    }

    public override byte[] ToByteArray()
    {
        var result = new byte[SystemPage.PageSize];

        StructSerializer.Serialize(result, 0, ref Header);
        Buffer.BlockCopy(
            Keys, 
            0, 
            result, 
            LeafPageHeader.LengthBytes, 
            Header.KeyCount * sizeof(int));
        
        var valuesSpan = Values.AsSpan(0, Header.KeyCount);
        var bytes = MemoryMarshal.AsBytes(valuesSpan);
        bytes.CopyTo(result.AsSpan(LeafPageHeader.LengthBytes + MaxKeys * sizeof(int)));
        
        return result;
    }
    
    public new static LeafPage FromByteArray(Span<byte> bytes)
    {
        var header = StructSerializer.Deserialize<LeafPageHeader>(bytes[..LeafPageHeader.LengthBytes]);
        if (header.PageType != PageType.LeafPage)
            throw new Exception("Invalid page type");
        
        var keys = new int[MaxKeys];
        var values = new (int PageId, int SlotId)[MaxKeys];
        
        var keysByteSize = MaxKeys * sizeof(int);
        var keyIndex = 0;
        for (int i = LeafPageHeader.LengthBytes; i < keysByteSize + LeafPageHeader.LengthBytes; i += sizeof(int))
        {
            var key = BitConverter.ToInt32(bytes[i..(i + sizeof(int))]);
            keys[keyIndex] = key;

            var pageIdPos = i + keysByteSize + keyIndex * sizeof(int);
            var slotIdPos = pageIdPos + sizeof(int);
            var pageId = BitConverter.ToInt32(bytes[pageIdPos..(pageIdPos + sizeof(int))]);
            var slotId = BitConverter.ToInt32(bytes[slotIdPos..(slotIdPos + sizeof(int))]);
            values[keyIndex++] = (pageId, slotId);
        }
        
        return new LeafPage(header, keys, values); 
    }

    public new static LeafPage CreateEmpty(int id) => new LeafPage(id);
}