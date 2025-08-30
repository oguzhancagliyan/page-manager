using PageManager.Domain.Entities;

namespace PageManager.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<PageDraft> PageDrafts => Set<PageDraft>();
    public DbSet<PagePublished> PagePublished => Set<PagePublished>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Page>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SiteId, x.Slug }).IsUnique();
            e.Property(x => x.Slug).IsRequired();
            
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            {
                e.Property(p => p.Version).IsRowVersion();
            }
            else
            {
                e.Ignore(p => p.Version);
            }
        });

        modelBuilder.Entity<PageDraft>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PageId, x.DraftNumber }).IsUnique();
            e.HasOne(x => x.Page)
                .WithMany(p => p.Drafts)
                .HasForeignKey(x => x.PageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PagePublished>(e =>
        {
            e.HasKey(x => x.PageId);
            e.HasOne(x => x.Page)
                .WithOne(p => p.Published)
                .HasForeignKey<PagePublished>(x => x.PageId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Draft)
                .WithMany()
                .HasForeignKey(x => x.DraftId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}