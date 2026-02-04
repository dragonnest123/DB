using System.Runtime.InteropServices;
using AzotBase.Page.Header;
using AzotBase.Utils;

namespace AzotBase.Page;

public class LeafPage : BPlusTreePage<LeafPageHeader>, IPage<LeafPage>
{
    public new static readonly int MaxKeys = (SystemPage.PageSize - LeafPageHeader.LengthBytes - sizeof(int)) / (3 * sizeof(int));
    
    public (int PageId, int SlotId)[] Values;
    
    public LeafPage(int id) : base(new LeafPageHeader(id), MaxKeys)
    {
        Values = new (int PageId, int SlotId)[MaxKeys];
        Header.IsLeaf = 1;
    }
    
    private LeafPage(LeafPageHeader header, int[] keys,  (int PageId, int SlotId)[] values) : base(header, keys, MaxKeys)
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
    }

    public void DeleteKey(int key)
    {
        int idx = FindKey(key);
        if (idx < 0) 
            return;
        
        for (int i = idx; i < Header.KeyCount - 1; i++)
        {
            Keys[i] = Keys[i + 1];
            Values[i] = Values[i + 1];
        }
        
        Header.KeyCount--;
    }
    
    public byte[] ToByteArray()
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
    
    public static LeafPage FromByteArray(Span<byte> bytes)
    {
        var header = StructSerializer.Deserialize<LeafPageHeader>(bytes[..LeafPageHeader.LengthBytes]);
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
}