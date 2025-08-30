namespace PageManager.Domain.Entities;

public class PagePublished
{
    public Guid PageId { get; set; }
    public Guid DraftId { get; set; }
    public DateTime PublishedUtc { get; set; }

    public Page? Page { get; set; }
    public PageDraft? Draft { get; set; }
}