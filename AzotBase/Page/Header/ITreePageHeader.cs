namespace AzotBase.Page.Header;

public interface ITreePageHeader
{
    public int Id { get; set; }
    public PageType PageType { get; set; }
    public int ParentId { get; set; }
    public int KeyCount { get; set; }
    public byte IsLeaf { get; set; }
}