namespace PageManager.Application.Features.Pages.GetPublishedPage;

public sealed record GetPublishedPageQuery(Guid SiteId, string Slug) : IRequest<PublishedPageDto?>;