namespace PageManager.Shared.Cache.Keys;

public static class CacheKeys
{
    private static string PublishedPages => "published_pages_{0}_{1}";

    public static string GetPublishedPagesKey(Guid siteId, string slug) => string.Format(PublishedPages, siteId, slug);
}