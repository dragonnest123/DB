using AzotBase.Common.Serialization.Attributes;

namespace AzotName.Tests.Common.Serialization;

public class SimpleTestRecord
{
    [SerializeOrder(0)] public int Id { get; set; }
    [SerializeOrder(1)] public string? Name { get; set; }
    [SerializeOrder(2)] public int Age { get; set; }
}

public class ComplexTestRecord
{
    public class NestedClass
    {
        public int Value { get; set; }
        public NestedStruct Struct { get; set; }

        public NestedClass()
        {
            Struct = new NestedStruct { Value = 5 };
        }
    }

    public struct NestedStruct
    {
        public int Value { get; set; }
    }

    public enum NestedEnum
    {
        EnumValue1,
        EnumValue2,
        EnumValue3
    }
    
    public int? Id { get; set; }
    public string? Name { get; set; }
    public int? Age { get; set; }
    public bool? Boolean { get; set; }
    public NestedEnum? Enum { get; set; }
    public NestedClass? Class { get; set; }
    public NestedStruct Struct { get; set; }
}