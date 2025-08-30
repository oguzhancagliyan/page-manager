var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "PagesApi");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db);
}

app.MapDelete("/api/v1/sites/{siteId:guid}/pages/{slug}",
    async Task<Results<NoContent, NotFound, BadRequest, Conflict>> (
        Guid siteId,
        string slug,
        int? publishDraft,
        ISender sender,
        CancellationToken ct) =>
    {
        try
        {
            await sender.Send(new ArchiveAndMayPublishCommand(siteId, slug, publishDraft), ct);
            return TypedResults.NoContent();
        }
        catch (NotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (ValidationException)
        {
            return TypedResults.BadRequest();
        }
        catch (ConflictException)
        {
            return TypedResults.Conflict();
        }
    });

app.MapGet("/api/v1/sites/{siteId:guid}/pages/{slug}/published",
    async Task<Results<Ok<PublishedPageDto>, NotFound>> (Guid siteId, string slug, ISender sender,
        CancellationToken ct) =>
    {
        var dto = await sender.Send(new GetPublishedPageQuery(siteId, slug), ct);
        return dto is null ? TypedResults.NotFound() : TypedResults.Ok(dto);
    });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var httpsEnabled = builder.Configuration.GetValue<bool>("Https:Enabled", false);
if (httpsEnabled)
{
    app.UseHttpsRedirection();
}

app.Run();