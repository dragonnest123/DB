namespace AzotBase.Storage.FSM;

public class FSMManager
{
    private readonly FileStream _fileStream;
    
    public FSMManager(FileStream fileStream)
    {
        _fileStream = fileStream;
    }

    public async Task<int> FindSuitablePage(int pageFreeSpace)
    {
        int blockId = 0;
        FSMBlock? currentBlock = await ReadBlockAsync(blockId);

        while (currentBlock != null)
        {
            var pageId = currentBlock.FindLocalPageId(pageFreeSpace);
            if (pageId != -1)
                return blockId * FSMBlock.MaxPagesCount + pageId;
            
            currentBlock = await ReadBlockAsync(++blockId);
        }
        
        return -1;  
    }
    
    //TODO: Реализовать логику обновления

    private async Task<FSMBlock?> ReadBlockAsync(int blockId)
    {
        var blockData = new byte[FSMBlock.BlockSize];
        
        var bytesRead = await RandomAccess.ReadAsync(
            _fileStream.SafeFileHandle, 
            blockData, 
            FSMBlock.BlockSize * blockId + sizeof(int));
        
        return bytesRead == 0 ? null : new FSMBlock(blockData);
    }
}