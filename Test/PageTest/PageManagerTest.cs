using AzotBase.Page;
using AzotBase.Utils;
using Test.Utils;

namespace Test.PageTest;

public class PageManagerTest
{
    private readonly PageManager _manager = PageManagerFactory.Create();
    
    [Fact]
    public async Task AllocatePage_ShouldCreateAndPersistPage()
    {
        var page = await _manager.AllocatePage<DataPage>();
        int id = page.Header.Id;
        
        Assert.Equal(0, id);
        
        var read = await _manager.LoadPage<DataPage>(id);
        
        Assert.Equal(id, read.Header.Id);
    }
    
    [Fact]
    public async Task WritePage_ShouldPersistChanges()
    {
        DataPage page = await _manager.AllocatePage<DataPage>();
        
        var record = new TestRecord { Name = "GADASDKvadkakdaksaadk", Age = 5, Id = 23 };
        var recordBytes = ClassSerializer.Serialize(record);

        var slotId = page.WriteRecord(recordBytes).slotID;
        await _manager.WritePage(page, page.Header.Id);
        
        var read =  await _manager.LoadPage<DataPage>(page.Header.Id);

        Assert.Equal(recordBytes, read.GetRecord(slotId));
        Assert.Equal(0, read.IsDirty);
    }
    
    [Fact]
    public async Task ReadPageType_ShouldReturnCorrectType()
    {
        var dataPage = await _manager.AllocatePage<DataPage>();
        var indexPage = await _manager.AllocatePage<IndexPage>();
        var leafPage = await _manager.AllocatePage<LeafPage>();

        var type1 = await _manager.ReadPageType(dataPage.Header.Id);
        var type2 = await _manager.ReadPageType(indexPage.Header.Id);
        var type3 = await _manager.ReadPageType(leafPage.Header.Id);

        Assert.Equal(dataPage.Header.PageType, type1);
        Assert.Equal(indexPage.Header.PageType, type2);
        Assert.Equal(leafPage.Header.PageType, type3);
    }
    
    [Fact]
    public async Task AllocateMultiplePages_ShouldIncreasePageId()
    {
        var p1 = await _manager.AllocatePage<DataPage>();
        var p2 = await _manager.AllocatePage<DataPage>();

        Assert.Equal(0, p1.Header.Id);
        Assert.Equal(1, p2.Header.Id);
    }
}