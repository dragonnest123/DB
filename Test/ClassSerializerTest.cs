using AzotBase.Utils;
using Test.PageTest;

namespace Test;

public class ClassSerializerTest
{
    [Fact]
    public void SerializeClass()
    {
        var c = new TestRecord
        {
            Id = 23,
            Name = "Test",
            Age = 141
        };
        
        var serialized = ClassSerializer.Serialize(c);
        var actual = new MemoryStream();
        var writer = new BinaryWriter(actual);
        
        writer.Write(true);
        writer.Write(c.Id);
        writer.Write(true);
        writer.Write(c.Name);
        writer.Write(true);
        writer.Write(c.Age);

        var a = serialized.SequenceEqual(actual.ToArray());
        Assert.True(a);
    }
    
    [Fact]
    public void SerializeWithNullClass()
    {
        var c = new TestRecord
        {
            Id = 23,
            Age = 141
        };
        
        var serialized = ClassSerializer.Serialize(c);
        var actual = new MemoryStream();
        var writer = new BinaryWriter(actual);
        
        writer.Write(true);
        writer.Write(c.Id);
        writer.Write(false);
        writer.Write(true);
        writer.Write(c.Age);

        var a = serialized.SequenceEqual(actual.ToArray());
        Assert.True(a);
    }

    [Fact]
    public void DeserializeClass()
    {
        var c = new TestRecord
        {
            Id = 23,
            Name = "Test",
            Age = 141
        };
        
        var serialized = ClassSerializer.Serialize(c);
        var deserialized = ClassSerializer.Deserialize<TestRecord>(serialized);
        
        Assert.Equal(c.Id, deserialized.Id);
        Assert.Equal(c.Name, deserialized.Name);
        Assert.Equal(c.Age, deserialized.Age);
    }
    
    [Fact]
    public void DeserializeWithNullClass()
    {
        var c = new TestRecord
        {
            Id = 23,
            Age = 141
        };
        
        var serialized = ClassSerializer.Serialize(c);
        var deserialized = ClassSerializer.Deserialize<TestRecord>(serialized);
        
        Assert.Equal(c.Id, deserialized.Id);
        Assert.Equal(c.Name, deserialized.Name);
        Assert.Equal(c.Age, deserialized.Age);
    }
}