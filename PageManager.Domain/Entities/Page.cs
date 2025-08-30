using System.ComponentModel.DataAnnotations;

namespace PageManager.Domain.Entities;

public class Page
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Slug { get; set; } = default!;
    public bool IsArchived { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public ICollection<PageDraft> Drafts { get; set; } = new List<PageDraft>();
    public PagePublished? Published { get; set; }
    
    [Timestamp]
    public uint Version { get; set; }
}