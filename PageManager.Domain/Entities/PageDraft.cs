namespace PageManager.Domain.Entities;

public class PageDraft
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public int DraftNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public Page? Page { get; set; }
}