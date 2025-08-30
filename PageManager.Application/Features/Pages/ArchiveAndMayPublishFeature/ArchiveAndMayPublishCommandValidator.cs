namespace PageManager.Application.Features.Pages.ArchiveAndMayPublishFeature;

public sealed class ArchiveAndMayPublishCommandValidator : AbstractValidator<ArchiveAndMayPublishCommand>
{
    public ArchiveAndMayPublishCommandValidator()
    {
        RuleFor(c => c.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(c => c.Slug).NotEmpty().WithMessage("Slug is required.");
        RuleFor(c => c.PublishDraft).GreaterThan(0).When(c => c.PublishDraft.HasValue)
            .WithMessage("PublishDraft must be greater than 0 if provided.");
    }
}