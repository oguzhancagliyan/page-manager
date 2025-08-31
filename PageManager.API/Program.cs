using PageManager.API.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "PagesApi");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Migration/Seeding failed.");
        throw;
    }
}

app.MapDelete("/api/v1/sites/{siteId:guid}/pages/{slug}",
        async Task<Results<NoContent, NotFound, BadRequest, Conflict>> (
            Guid siteId,
            string slug,
            int? publishDraft,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ArchiveAndMayPublishCommand(siteId, slug, publishDraft), ct);
            return TypedResults.NoContent();
        }).Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status500InternalServerError);

app.MapGet("/api/v1/sites/{siteId:guid}/pages/{slug}/published",
        async Task<Results<Ok<PublishedPageDto>, NotFound>> (Guid siteId, string slug, ISender sender,
            CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetPublishedPageQuery(siteId, slug), ct);
            return dto is null ? TypedResults.NotFound() : TypedResults.Ok(dto);
        }).Produces<PublishedPageDto>()
    .ProducesProblem(StatusCodes.Status404NotFound);
;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

var httpsEnabled = builder.Configuration.GetValue<bool>("Https:Enabled", false);
if (httpsEnabled)
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();

app.Run();