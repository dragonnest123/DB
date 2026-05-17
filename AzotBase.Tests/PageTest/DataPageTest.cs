using AzotBase.Common.Serialization;
using AzotBase.Storage.Pages;

namespace AzotName.Tests.PageTest;

public class DataPageTest
{
    private TestRecord _record16Bytes = new TestRecord
    {
        Id = 352,
        Name = "AzotBase.Tests",
        Age = 231
    };
    private TestRecord _record32Bytes = new TestRecord
    {
        Id = 35231,
        Name = "DSFSDKFKSDFKSKsdsadd",
        Age = 1111111111
    };
    
    [Fact]
    public void Page_WriteReadRecord()
    {
        var page = new DataPage(1);

        var bytes1 = ClassSerializer.Serialize(_record16Bytes);
        var bytes2 = ClassSerializer.Serialize(_record32Bytes);
        
        page.WriteRecord(bytes1);
        page.WriteRecord(bytes2);

        var read1 = page.GetRecord(0);
        var read2 = page.GetRecord(1);
        
        Assert.True(bytes1.SequenceEqual(read1.ToArray()));
        Assert.True(bytes2.SequenceEqual(read2.ToArray()));
    }
}