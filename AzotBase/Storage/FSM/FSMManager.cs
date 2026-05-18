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
                return ConvertToGlobalPageId(blockId, pageId);
            
            currentBlock = await ReadBlockAsync(++blockId);
        }
        
        return -1;  
    }

    public async Task UpdatePageFreeSpace(int pageId, int pageFreeSpace)
    {
        var blockId = ConvertToBlockId(pageId);
        var block = await ReadBlockAsync(blockId) ?? throw new Exception($"Incorrect block id {blockId}");
        
        var localPageId = ConvertToLocalPageId(pageId);
        block.Update(localPageId, pageFreeSpace);
        
        await WriteBlockAsync(blockId, block);
    }
    
    private async Task<FSMBlock?> ReadBlockAsync(int blockId)
    {
        var blockData = new byte[FSMBlock.BlockSizeBytes];
        
        var bytesRead = await RandomAccess.ReadAsync(
            _fileStream.SafeFileHandle, 
            blockData, 
            FSMBlock.BlockSizeBytes * blockId + sizeof(int));
        
        return bytesRead == 0 ? null : new FSMBlock(blockData);
    }

    private ValueTask WriteBlockAsync(int blockId, FSMBlock block)
    {
        var blockData = block.ToByteArray();
        
        return RandomAccess.WriteAsync(
            _fileStream.SafeFileHandle, 
            blockData, 
            blockId * FSMBlock.BlockSizeBytes);
    }

    private static int ConvertToBlockId(int globalPageId) 
        => globalPageId / FSMBlock.MaxPagesPerBlock;
    
    private static int ConvertToGlobalPageId(int blockId, int localPageId)
        => blockId * FSMBlock.MaxPagesPerBlock + localPageId;
    
    private static int ConvertToLocalPageId(int globalPageId)
        => globalPageId % FSMBlock.MaxPagesPerBlock;
}