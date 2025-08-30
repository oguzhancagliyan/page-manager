namespace PageManager.Application.Features.Pages.GetPublishedPage;

public sealed class GetPublishedPageHandler(
    ICacheManager cache,
    AppDbContext db,
    ILogger<GetPublishedPageHandler> logger)
    : IRequestHandler<GetPublishedPageQuery, PublishedPageDto?>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan NegTtl = TimeSpan.FromSeconds(10);

    public async Task<PublishedPageDto?> Handle(GetPublishedPageQuery request, CancellationToken ct)
    {
        var key = CacheKeys.GetPublishedPagesKey(request.SiteId, request.Slug);

        return await cache.GetOrSetAsync(
            key,
            async token =>
            {
                var page = await db.Pages
                    .Include(p => p.Published)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.SiteId == request.SiteId && p.Slug == request.Slug, token);

                return page?.Published is null
                    ? null
                    : new PublishedPageDto(page.Id, page.Published.DraftId, page.Published.PublishedUtc);
            },
            ttl: Ttl,
            negativeTtl: NegTtl,
            ct);
    }
}