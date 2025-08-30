using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PageManager.Application.Features.Pages.ArchiveAndMayPublishFeature;
using PageManager.Domain.Entities;
using PageManager.Infrastructure.Cache;
using PageManager.Infrastructure.Persistence;
using PageManager.Shared.Exceptions;
using PageManager.UnitTests.Common;

namespace PageManager.UnitTests.Handlers;

public class ArchiveAndMayPublishMinimalCaseTests
{
    private static async Task SeedAsync(AppDbContext db, Guid siteId, string slug, int drafts = 2,
        int? publishedDraft = null)
    {
        var page = new Page
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            Slug = slug,
            IsArchived = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedUtc = DateTime.UtcNow.AddDays(-1),
        };

        for (var i = 1; i <= drafts; i++)
            page.Drafts.Add(new PageDraft { Id = Guid.NewGuid(), PageId = page.Id, DraftNumber = i });

        if (publishedDraft is int pd)
        {
            var chosen = page.Drafts.Single(d => d.DraftNumber == pd);
            page.Published = new PagePublished
                { PageId = page.Id, DraftId = chosen.Id, PublishedUtc = DateTime.UtcNow.AddHours(-2) };
            db.PagePublished.Add(page.Published);
        }

        db.Pages.Add(page);
        await db.SaveChangesAsync();
    }

    private static ArchiveAndMayPublishHandler.Handler MakeHandler(
        AppDbContext db,
        out Mock<ICacheManager> cacheMock)
    {
        cacheMock = new Mock<ICacheManager>(MockBehavior.Loose);
        cacheMock.Setup(c => c.Invalidate(It.IsAny<string>()));
        var validator = new ArchiveAndMayPublishCommandValidator();
        var logger = new LoggerFactory().CreateLogger<ArchiveAndMayPublishHandler>();

        return new ArchiveAndMayPublishHandler.Handler(db, cacheMock.Object, logger, validator);
    }

    [Fact]
    public async Task Idempotency_DELETE_publish_twice_single_publish_archived_204_both()
    {
        using var conn = DbHelper.OpenConnection();
        DbHelper.EnsureCreated(conn);

        var options = DbHelper.Options(conn);

        var siteId = Guid.NewGuid();
        const string slug = "home";
        await using (var seedDb = new AppDbContext(options))
            await SeedAsync(seedDb, siteId, slug, drafts: 2, publishedDraft: null);

        await using (var db1 = new AppDbContext(options))
        {
            var handler = MakeHandler(db1, out _);
            await handler.Handle(new ArchiveAndMayPublishCommand(siteId, slug, 1), default);
        }

        await using (var db2 = new AppDbContext(options))
        {
            var handler = MakeHandler(db2, out _);
            await handler.Handle(new ArchiveAndMayPublishCommand(siteId, slug, 1), default);
        }

        await using (var readDb = new AppDbContext(options))
        {
            var page = await readDb.Pages.Include(p => p.Published).Include(p => p.Drafts)
                .AsNoTracking()
                .FirstAsync(p => p.SiteId == siteId && p.Slug == slug);

            page.IsArchived.Should().BeTrue();
            page.Published.Should().NotBeNull();
            var draft1Id = page.Drafts.Single(d => d.DraftNumber == 1).Id;
            page.Published!.DraftId.Should().Be(draft1Id);
        }
    }

    [Fact]
    public async Task Concurrency_two_overlapping_DELETE_publish_one_success_other_409_after_retry()
    {
        await using var conn = DbHelper.OpenConnection();
        DbHelper.EnsureCreated(conn);
        var options = DbHelper.Options(conn);

        var siteId = Guid.NewGuid();
        const string slug = "concurrency";

        await using (var seedDb = new AppDbContext(options))
            await SeedAsync(seedDb, siteId, slug, drafts: 2);

        await using (var dbA = new ThrowOnceConcurrencyContext(options))
        {
            var handlerA = MakeHandler(dbA, out _);
            await handlerA.Handle(
                new ArchiveAndMayPublishCommand(siteId, slug, 2),
                default);
        }

        await using (var dbB = new AlwaysFailConcurrencyContext(options))
        {
            var handlerB = MakeHandler(dbB, out _);
            var act = () => handlerB.Handle(
                new ArchiveAndMayPublishCommand(siteId, slug, 2),
                default);

            await act.Should().ThrowAsync<ConflictException>();
        }

        await using var checkDb = new AppDbContext(options);
        var page = await checkDb.Pages.Include(p => p.Published).Include(p => p.Drafts)
            .AsNoTracking()
            .FirstAsync(p => p.SiteId == siteId && p.Slug == slug);

        page.IsArchived.Should().BeTrue();
        var d2 = page.Drafts.Single(d => d.DraftNumber == 2).Id;
        page.Published!.DraftId.Should().Be(d2);
    }
}