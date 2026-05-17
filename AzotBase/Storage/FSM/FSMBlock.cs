using AzotBase.Storage.Pages;

namespace AzotBase.Storage.FSM;

public class FSMBlock
{
    public const ushort BlockSize = 1 + ChildCount + ChildCount * ChildCount;
    public const int MaxPagesCount = ChildCount * ChildCount;
    
    private const int ChildCount = 64; //PageCount
    private const int LeavesOffset = ChildCount + 1;

    private readonly byte[] _pageSizeCategories;

    public FSMBlock(byte[] blockData)
    {
        _pageSizeCategories = blockData;
    }

    public int FindLocalPageId(int pageFreeSpace)
    {
        var category = GetCategory(pageFreeSpace);
        
        if (_pageSizeCategories[0] < category)
            return -1;

        int index = 0;
        while (index < LeavesOffset)
        {
            int firstChild = index * ChildCount + 1;
            int found = -1;

            for (int i = 0; i < ChildCount; i++)
            {
                if (category <= _pageSizeCategories[firstChild + i])
                {
                    found = firstChild + i;
                    break;
                }
            }
            if (found == -1)
                return -1;
            
            index = found;
        }
        
        return index - LeavesOffset;
    }

    public void Update(int localPageId, int newFreeSpace)
    {
        var index = localPageId + LeavesOffset;
        _pageSizeCategories[index] = GetCategory(newFreeSpace);

        while (index > 0)
        {
            index = (index - 1) / ChildCount;
            int firstChild = index * ChildCount + 1;
            
            byte max = 0;
            for (int i = 0; i < ChildCount; i++)
                max = Math.Max(max, _pageSizeCategories[firstChild + i]);    
            
            _pageSizeCategories[index] = max;
        }
    }

    private byte GetCategory(int freeSpace)
        => (byte)(freeSpace * byte.MaxValue / SystemPage.PageSize);
}