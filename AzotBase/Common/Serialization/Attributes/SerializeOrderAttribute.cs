namespace AzotBase.Common.Serialization.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class SerializeOrderAttribute : Attribute
{
    public int Order { get; }
    public SerializeOrderAttribute(int order) => Order = order;
}