namespace AzotBase.Page;

public class FSMBlock
{
    public const ushort BlockSize = 1 + ChildCount + ChildCount * ChildCount;
    private const int ChildCount = 64; //PageCount
    private const int LeavesOffset = ChildCount + 1;

    private byte[] _pageSizeCategories = new byte[BlockSize];

    public int FindSuitablePage(int freeSpace)
    {
        var category = GetCategory(freeSpace);
        
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
        
        return index;
    }

    private void UpdateFSMBlock()
    {
        
    }

    private byte GetCategory(int freeSpace)
        => (byte)(freeSpace * byte.MaxValue / SystemPage.PageSize);
}