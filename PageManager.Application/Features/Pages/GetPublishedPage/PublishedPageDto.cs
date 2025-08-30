namespace PageManager.Application.Features.Pages.GetPublishedPage;
public sealed record PublishedPageDto(Guid PageId, Guid DraftId, DateTime PublishedUtc);