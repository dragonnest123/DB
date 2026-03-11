using System.Collections.Specialized;
using AzotBase.Page.Header;
using AzotBase.Utils;

namespace AzotBase.Page;

public class IndexPage : BPlusTreePage<IndexPageHeader>, IPage<IndexPage>
{
    public new static readonly int MaxKeys = (SystemPage.PageSize - IndexPageHeader.LengthBytes - sizeof(int)) / (2 * sizeof(int));
    
    public override int Id => Header.Id;
    public int[] ChildrenPageIds = new int[MaxKeys + 2]; //DISK
    
    public IndexPage(int id) : base(new IndexPageHeader(id), MaxKeys + 1)
    {
        for (int i = 0; i < ChildrenPageIds.Length; i++)
            ChildrenPageIds[i] = -1;
    }

    private IndexPage(IndexPageHeader header, int[] keys, int[] childrenPageIds) : base(header, keys, MaxKeys + 1)
    {
        ChildrenPageIds = childrenPageIds;
    }
    
    public void InsertKey(int key, int rightPageId)
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
        ChildrenPageIds[pos + 1] = rightPageId;
        
        Header.KeyCount++;
        IsDirty = 1;
    }

    public void InsertKeyAt(int index, int key, int rightPageId)
    {
        int keysToMove = Header.KeyCount - index;
        
        Array.Copy(Keys, index, Keys, index + 1, keysToMove);
        Keys[index] = key;
        
        Array.Copy(ChildrenPageIds, index + 1, ChildrenPageIds, index + 2, keysToMove);
        ChildrenPageIds[index + 1] = rightPageId;
        
        Header.KeyCount++;
        IsDirty = 1;
    }
    
    public void InsertRangeAt(int index, Span<int> keys, Span<int> children)
    {
        keys.CopyTo(Keys.AsSpan()[index..]);
        children.CopyTo(ChildrenPageIds.AsSpan()[index..]);

        Header.KeyCount += keys.Length;
        IsDirty = 1;
    }
    
    public void DeleteKey(int key)  
    {
        int idx = FindKey(key);
        if (idx < 0) 
            return;

        int keysToMove = Header.KeyCount - (idx + 1);
        if (keysToMove > 0)
        {
            Array.Copy(Keys, idx + 1, Keys, idx, keysToMove);
            Array.Copy(ChildrenPageIds, idx + 2, ChildrenPageIds, idx + 1, keysToMove);
        }
        
        Header.KeyCount--;
        IsDirty = 1;
    }

    public void DeleteKeyAt(int index)
    {
        int keysToMove = Header.KeyCount - (index + 1);
        
        Array.Copy(Keys, index + 1, Keys, index, keysToMove);
        Array.Copy(ChildrenPageIds, index + 2, ChildrenPageIds, index + 1, keysToMove);
        
        Header.KeyCount--;
        IsDirty = 1;
    }

    public void ReplaceKeyAt(int index, int newKey)
    {
        Keys[index] = newKey;
        IsDirty = 1;
    }

    public override byte[] ToByteArray()
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
            IndexPageHeader.LengthBytes + (MaxKeys) * sizeof(int), 
            (Header.KeyCount + 1) * sizeof(int));
        
        return result;
    }
    
    public static IndexPage FromByteArray(Span<byte> bytes)
    {
        var header = StructSerializer.Deserialize<IndexPageHeader>(bytes[..IndexPageHeader.LengthBytes]);
        if (header.PageType != PageType.IndexPage)
            throw new Exception("Invalid page type");
        
        var keys = new int[MaxKeys];
        var childrenPageIds = new int[MaxKeys + 1];
        
        var keysByteSize = MaxKeys * sizeof(int);
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

    public static IndexPage CreateEmpty(int id) => new IndexPage(id);
}