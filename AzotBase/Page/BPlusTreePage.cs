using System.Data.SqlTypes;
using AzotBase.Page.Header;

namespace AzotBase.Page;

public abstract class BPlusTreePage<THeader> : PageBase where THeader : unmanaged, ITreePageHeader
{
    public THeader Header;
    public int[] Keys;
    public int MaxKeys;

    protected BPlusTreePage(THeader header, int maxKeys)
    {
        Header = header;
        MaxKeys = maxKeys;
        Keys = new int[maxKeys];
    }
    
    protected BPlusTreePage(THeader header, int[] keys, int maxKeys)
    {
        Header = header;
        Keys = keys;
        MaxKeys = maxKeys;
    }
    
    public int FindKey(int key)
    {
        int left = 0, right = Header.KeyCount - 1;
        
        while (left <= right)
        {
            int mid = (left + right) / 2;
            if (key < Keys[mid]) 
                right = mid - 1;
            else if (key > Keys[mid]) 
                left = mid + 1;
            else 
                return mid;
        }
        return -1;
    }
}