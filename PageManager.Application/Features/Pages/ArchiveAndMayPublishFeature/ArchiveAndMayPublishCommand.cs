namespace PageManager.Application.Features.Pages.ArchiveAndMayPublishFeature;

public sealed record ArchiveAndMayPublishCommand(Guid SiteId, string Slug, int? PublishDraft) : IRequest;