using AzotBase.Storage.Pages.Common;

namespace AzotBase.Storage.Pages.Headers;

public interface IPageHeader
{
    public int Id { get; set; }
    public PageType PageType { get; set; }
}