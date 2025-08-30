using System.Security.Cryptography;
using System.Text;
using PageManager.Domain.Entities;

namespace PageManager.Infrastructure.Persistence;

public static class DbSeeder
{
    private static readonly Guid DemoSiteId = Guid.Parse("5abf2c3f-4fb9-4a19-a806-178139b73651");
    
    private static class DeterministicGuid
    {
        public static Guid FromString(string input)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return new Guid(bytes);
        }
    }

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {

        db.Pages.RemoveRange(db.Pages.ToList());
        db.PageDrafts.RemoveRange(db.PageDrafts.ToList());
        db.PagePublished.RemoveRange(db.PagePublished.ToList());
        await db.SaveChangesAsync(ct);
        
        var seedBaseUtc = DateTime.UtcNow;
        
        string[] slugs =
        [
            "home", "about", "contact", "products", "services", "blog", "news", "faq",
            "careers", "privacy", "terms", "pricing", "features", "integrations", "partners", "press",
            "support", "status", "docs", "api", "changelog", "roadmap", "refunds", "gdpr"
        ];

        var pages = new List<Page>(slugs.Length);
        var drafts = new List<PageDraft>(slugs.Length * 2);
        var published = new List<PagePublished>(slugs.Length);

        for (int i = 0; i < slugs.Length; i++)
        {
            var slug = slugs[i];
            var pageId = DeterministicGuid.FromString($"page:{DemoSiteId}:{slug}");

            var isArchived = i is 7 or 18; // örn. faq ve docs arşivlenmiş gösterelim
            var created = seedBaseUtc.AddDays(-(i + 1));
            var updated = created.AddHours(6 + (i % 5));

            var page = new Page
            {
                Id = pageId,
                SiteId = DemoSiteId,
                Slug = slug,
                IsArchived = isArchived,
                CreatedUtc = created,
                UpdatedUtc = updated
            };
            pages.Add(page);
            
            var draft1Id = DeterministicGuid.FromString($"draft:{slug}:1");
            var draft2Id = DeterministicGuid.FromString($"draft:{slug}:2");

            var draft1 = new PageDraft
            {
                Id = draft1Id,
                PageId = pageId,
                DraftNumber = 1,
                Content = $"{slug} draft #1 content"
            };

            var draft2 = new PageDraft
            {
                Id = draft2Id,
                PageId = pageId,
                DraftNumber = 2,
                Content = $"{slug} draft #2 content"
            };

            drafts.Add(draft1);
            drafts.Add(draft2);
            
            if ((i + 1) % 6 == 0)
            {
                continue;
            }

            var chosenDraftId = (i % 2 == 0) ? draft1Id : draft2Id;

            published.Add(new PagePublished
            {
                PageId = pageId,
                DraftId = chosenDraftId,
                PublishedUtc = seedBaseUtc.AddHours(-(i + 2))
            });
        }

        db.Pages.AddRange(pages);
        db.PageDrafts.AddRange(drafts);
        db.PagePublished.AddRange(published);

        await db.SaveChangesAsync(ct);
    }
}
