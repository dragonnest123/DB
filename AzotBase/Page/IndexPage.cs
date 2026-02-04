using System.Collections.Specialized;
using AzotBase.Page.Header;
using AzotBase.Utils;

namespace AzotBase.Page;

public class IndexPage : BPlusTreePage<IndexPageHeader>, IPage<IndexPage>
{
    public static readonly int MaxChildren = (SystemPage.PageSize - IndexPageHeader.LengthBytes - sizeof(int)) / (2 * sizeof(int));
    
    public int[] ChildrenPageIds = new int[MaxChildren];
    
    public IndexPage(int id) : base(new IndexPageHeader(id), MaxChildren - 1)
    {
        for (int i = 0; i < ChildrenPageIds.Length; i++)
            ChildrenPageIds[i] = -1;
    }

    private IndexPage(IndexPageHeader header, int[] keys, int[] childrenPageIds) : base(header, keys, MaxChildren - 1)
    {
        ChildrenPageIds = childrenPageIds;
    }
    
    public void InsertKey(int key, int leftPageId, int rightPageId)
    {
        int i = Header.KeyCount - 1;
        while (i >= 0 && Keys[i] > key)
        {
            Keys[i + 1] = Keys[i];
            ChildrenPageIds[i + 2] = ChildrenPageIds[i + 1];
            i--;
        }
        
        int pos = i + 1;
        Keys[pos] = key;
        ChildrenPageIds[pos] = leftPageId;
        ChildrenPageIds[pos + 1] = rightPageId;
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
            ChildrenPageIds[i + 1] = ChildrenPageIds[i + 2];
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
            IndexPageHeader.LengthBytes, 
            Header.KeyCount * sizeof(int));
        Buffer.BlockCopy(
            ChildrenPageIds, 
            0, 
            result, 
            IndexPageHeader.LengthBytes + (MaxChildren - 1) * sizeof(int), 
            (Header.KeyCount + 1) * sizeof(int));
        
        return result;
    }
    
    public static IndexPage FromByteArray(Span<byte> bytes)
    {
        var header = StructSerializer.Deserialize<IndexPageHeader>(bytes[..IndexPageHeader.LengthBytes]);
        var keys = new int[MaxChildren - 1];
        var childrenPageIds = new int[MaxChildren];
        
        var keysByteSize = (MaxChildren - 1) * sizeof(int);
        var keyIndex = 0;
        for (int i = IndexPageHeader.LengthBytes; i < keysByteSize + IndexPageHeader.LengthBytes; i += sizeof(int))
        {
            var key = BitConverter.ToInt32(bytes[i..(i + sizeof(int))]);
            keys[keyIndex] = key;

            var childPos = i + keysByteSize;
            var childrenPageId = BitConverter.ToInt32(bytes[childPos..(childPos + sizeof(int))]);
            childrenPageIds[keyIndex++] = childrenPageId;
        }
        var lastChildPos = IndexPageHeader.LengthBytes + 2 * keysByteSize;
        childrenPageIds[keyIndex] = BitConverter.ToInt32(bytes[lastChildPos..(lastChildPos + sizeof(int))]);
        
        return new IndexPage(header, keys, childrenPageIds); 
    }
}