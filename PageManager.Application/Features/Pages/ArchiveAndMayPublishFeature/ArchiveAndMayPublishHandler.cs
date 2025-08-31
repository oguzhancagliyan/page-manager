namespace PageManager.Application.Features.Pages.ArchiveAndMayPublishFeature;

public class ArchiveAndMayPublishHandler
{
    private static readonly ActivitySource Activity = new("Feature.Pages.ArchiveAndMayPublish");

    private const int MaxAttempts = 2;

    public sealed class Handler(
        AppDbContext appContext,
        ICacheManager cacheManager,
        ILogger<ArchiveAndMayPublishHandler> logger,
        IValidator<ArchiveAndMayPublishCommand> validator)
        : IRequestHandler<ArchiveAndMayPublishCommand>
    {
        public async Task Handle(ArchiveAndMayPublishCommand request, CancellationToken cancellationToken)
        {
            using var activity = Activity.StartActivity("Handle.ArchiveAndMayPublish");

            activity?.SetTag("siteId", request.SiteId);
            activity?.SetTag("slug", request.Slug);
            activity?.SetTag("publishDraft", request.PublishDraft);

            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "ValidationFailed");
                foreach (var err in validationResult.Errors)
                    activity?.AddEvent(new ActivityEvent($"validation_error:{err.PropertyName}:{err.ErrorMessage}"));
                throw new ValidationException(validationResult.Errors);
            }

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    activity?.AddEvent(new ActivityEvent($"db.tx.begin attempt:{attempt}"));
                    await using var transaction = await appContext.Database.BeginTransactionAsync(cancellationToken);

                    var page = await appContext.Pages
                        .Include(p => p.Published)
                        .FirstOrDefaultAsync(a => a.SiteId == request.SiteId && a.Slug == request.Slug,
                            cancellationToken);

                    if (page is null)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, "PageNotFound");
                        activity?.AddEvent(new ActivityEvent("page_not_found"));
                        logger.LogError("Page not found. SiteId={SiteId}, Slug={Slug}", request.SiteId, request.Slug);
                        throw new NotFoundException(
                            $"Page with slug '{request.Slug}' not found for site '{request.SiteId}'");
                    }

                    activity?.SetTag("pageId", page.Id);

                    page.IsArchived = true;
                    page.UpdatedUtc = DateTime.UtcNow;
                    activity?.AddEvent(new ActivityEvent("page_archived"));

                    if (request.PublishDraft.HasValue)
                    {
                        await HandlePublishDraftAsync(page, request.PublishDraft.Value, cancellationToken);
                    }

                    await appContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    activity?.AddEvent(new ActivityEvent("db.tx.commit"));
                    var cacheKey = CacheKeys.GetPublishedPagesKey(request.SiteId, request.Slug);
                    try
                    {
                        cacheManager.Invalidate(cacheKey);
                        activity?.AddEvent(new ActivityEvent("cache.invalidate"));
                        activity?.SetStatus(ActivityStatusCode.Ok);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Cache invalidation failed for {Key}", cacheKey);
                        activity?.AddEvent(new ActivityEvent("cache.invalidate.failed"));
                    }

                    return;
                }
                catch (DbUpdateConcurrencyException ex) when (attempt < MaxAttempts)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "DbUpdateConcurrencyException");
                    activity?.AddException(ex);
                    activity?.AddEvent(new ActivityEvent("concurrency_retry"));
                    logger.LogWarning("Concurrency conflict on archive/publish. Attempt {Attempt}/{MaxAttempts}",
                        attempt, MaxAttempts);
                    appContext.ChangeTracker.Clear();
                    await Task.Delay(10, cancellationToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, "ConcurrencyConflict");
                    logger.LogWarning(ex, "Concurrency conflict after {MaxAttempts} attempts", MaxAttempts);
                    throw new ConflictException("Unable to complete operation due to concurrent modifications");
                }
                catch (Exception ex)
                {
                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    throw;
                }
            }

            activity?.SetStatus(ActivityStatusCode.Error, "MaxAttemptsExceeded");
            throw new ConflictException("Unable to complete operation due to concurrent modifications");
        }

        private async Task HandlePublishDraftAsync(Page page, int draftNumber, CancellationToken cancellationToken)
        {
            using var activity = Activity.StartActivity("Handle.HandlePublishDraftAsync");
            activity?.SetTag("draftNumber", draftNumber);

            var draft = await appContext.PageDrafts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PageId == page.Id && c.DraftNumber == draftNumber, cancellationToken);

            if (draft is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "InvalidDraftNumber");
                activity?.AddEvent(new ActivityEvent("draft_not_found"));
                throw new ValidationException([
                    new FluentValidation.Results.ValidationFailure("publishDraft",
                        $"Draft #{draftNumber} is not valid for PageId {page.Id}.")
                ]);
            }

            activity?.SetTag("draftId", draft.Id);

            if (page.Published is null)
            {
                page.Published = new PagePublished
                    { PageId = page.Id, DraftId = draft.Id, PublishedUtc = DateTime.UtcNow };
                appContext.PagePublished.Add(page.Published);
                activity?.AddEvent(new ActivityEvent("published_created"));
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            if (page.Published.DraftId != draft.Id)
            {
                page.Published.DraftId = draft.Id;
                page.Published.PublishedUtc = DateTime.UtcNow;
                activity?.AddEvent(new ActivityEvent("published_updated"));
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }
}