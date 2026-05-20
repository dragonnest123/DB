using AzotBase.Common.Serialization;
using FluentAssertions;

namespace AzotName.Tests.Common.Serialization;

public class ClassSerializerTest
{
    [Fact]
    public void Serialize_deserialize_class_with_primitive_types_returns_same_object()
    {
        var c = new SimpleTestRecord
        {
            Id = 23,
            Name = "AzotBase.Tests",
            Age = 141
        };
        
        var serialized = ClassSerializer.Serialize(c);
        var deserialized = ClassSerializer.Deserialize<SimpleTestRecord>(serialized);
        
        deserialized.Should().BeEquivalentTo(c);
    }
    
    [Fact]
    public void Serialize_deserialize_class_with_nullable_primitive_types_returns_same_object()
    {
        var c = new SimpleTestRecord
        {
            Id = 23,
            Name = null,
            Age = 141
        };
        
        var serialized = ClassSerializer.Serialize(c);
        var deserialized = ClassSerializer.Deserialize<SimpleTestRecord>(serialized);
        
        deserialized.Should().BeEquivalentTo(c);
    }
    
    [Fact]
    public void Serialize_deserialize_class_with_nested_class_and_struct_returns_same_object()
    {
        var c = new ComplexTestRecord
        {
            Id = 123,
            Name = "AzotBase.Tests",
            Age = 141,
            Boolean =  true,
            Enum = ComplexTestRecord.NestedEnum.EnumValue3,
            Class = new ComplexTestRecord.NestedClass { Value = 42 },
            Struct = new ComplexTestRecord.NestedStruct { Value = 99 }
        };

        var serialized = ClassSerializer.Serialize(c);
        var deserialized = ClassSerializer.Deserialize<ComplexTestRecord>(serialized);
        
        deserialized.Should().BeEquivalentTo(c);
    }
}