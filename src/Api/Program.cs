using KnowledgeSearch;
using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Domain.Sources;
using KnowledgeSearch.Core.UseCases.CodeGraph;
using KnowledgeSearch.Core.UseCases.Search;
using KnowledgeSearch.Core.UseCases.Sources;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5111");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var dbPath = Environment.GetEnvironmentVariable("KNOWLEDGE_DB")
    ?? builder.Configuration["KnowledgeDb"]
    ?? Path.GetFullPath("../knowledge.db");

var skillsDir = Environment.GetEnvironmentVariable("SKILLS_DIR")
    ?? builder.Configuration["SkillsDir"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");

var indexHtmlPath = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "index.html"))
    ? Path.Combine(Directory.GetCurrentDirectory(), "index.html")
    : Path.Combine(AppContext.BaseDirectory, "index.html");
var staticDir = Path.GetDirectoryName(indexHtmlPath)!;

var logPath = Path.ChangeExtension(dbPath, ".log");

var sourceConfigService = new SourceConfigurationService(builder.Configuration, Environment.GetEnvironmentVariable);
builder.Services.AddSingleton<ISourceConfigurationService>(sourceConfigService);
builder.Services.AddSingleton<ISourceConfigurationStore>(sourceConfigService);

var roots = sourceConfigService.GetConfiguration()
    .Sources
    .Select(s => s.ToConfiguredSource())
    .Where(s => s.IndexDocs)
    .Select(s => s.GetAccessiblePath())
    .ToList();

var dbService = new DbService(dbPath, roots);
builder.Services.AddSingleton<IDbService>(dbService);
builder.Services.AddSingleton<IDocumentIndex>(dbService);
builder.Services.AddSingleton<IDocumentSearchIndex, DocumentSearchIndexAdapter>();
builder.Services.AddSingleton<ILogService>(new LogService(logPath));
var fileWatcherMode = Environment.GetEnvironmentVariable("FILE_WATCHER")
    ?? builder.Configuration["FileWatcher"]
    ?? "fsw";

if (fileWatcherMode == "polling")
{
    builder.Services.AddSingleton<IFileChangeSource>(new PollingChangeSource(TimeSpan.FromSeconds(5)));
}
else
{
    builder.Services.AddSingleton<IFileChangeSource, FileSystemChangeSource>();
}

builder.Services.AddHostedService(serviceProvider =>
    new WatcherService(
        serviceProvider.GetRequiredService<ISourceConfigurationService>(),
        serviceProvider.GetRequiredService<IDbService>(),
        serviceProvider.GetRequiredService<ILogService>(),
        serviceProvider.GetRequiredService<IFileChangeSource>()));

builder.Services.AddSingleton<ICodeGraphRepository>(_ => new CodeGraphRepository(dbPath));
builder.Services.AddKeyedSingleton<ISourceFileParser, CSharpParser>(".cs");
foreach (var typeScriptExtension in new[] { ".ts", ".tsx", ".js", ".jsx", ".mjs" })
{
    builder.Services.AddKeyedSingleton<ISourceFileParser, TypeScriptParser>(typeScriptExtension);
}
builder.Services.AddKeyedSingleton<ISourceFileParser, PythonParser>(".py");
builder.Services.AddSingleton<ICodeGraphService, CodeGraphService>();
builder.Services.AddSingleton<ICodeGraphStore>(serviceProvider => serviceProvider.GetRequiredService<ICodeGraphRepository>());
builder.Services.AddSingleton<ICodeGraphSearchService>(serviceProvider => serviceProvider.GetRequiredService<ICodeGraphService>());
builder.Services.AddSingleton<ICodeGraphScanner, CodeGraphScannerAdapter>();
builder.Services.AddSingleton<IFileSystem, FileSystemAdapter>();
builder.Services.AddSingleton<GetSourcesUseCase>();
builder.Services.AddSingleton<SaveSourcesUseCase>();
builder.Services.AddSingleton<ExportSourcesUseCase>();
builder.Services.AddSingleton<SearchDocumentsUseCase>();
builder.Services.AddSingleton<IndexDocumentsUseCase>();
builder.Services.AddSingleton<GetDocumentFileUseCase>();
builder.Services.AddSingleton<SaveDocumentFileUseCase>();
builder.Services.AddSingleton<GetImageFileUseCase>();
builder.Services.AddSingleton<GetKnowledgeRootsUseCase>();
builder.Services.AddSingleton<GetHealthUseCase>();
builder.Services.AddSingleton<ListRepositoriesUseCase>();
builder.Services.AddSingleton<GetRepositoryGraphUseCase>();
builder.Services.AddSingleton<ScanRepositoryUseCase>();
builder.Services.AddSingleton<BuildCrossRepoRefsUseCase>();
builder.Services.AddSingleton<SearchCodeDocumentsUseCase>();
builder.Services.AddSingleton<SearchRepositorySubgraphUseCase>();
builder.Services.AddSingleton<SearchCrossRepoSubgraphUseCase>();
builder.Services.AddSingleton<GetCodeFileUseCase>();

var app = builder.Build();
var logger = app.Logger;

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var exception = feature?.Error;

#pragma warning disable CA1848 // LoggerMessage delegates not needed for one-off exception handler
    logger.LogError(exception, "Unhandled exception on {Method} {Path}",
        context.Request.Method, context.Request.Path);
#pragma warning restore CA1848

    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";

    var message = exception?.Message ?? "Error interno del servidor.";
    await context.Response.WriteAsJsonAsync(
        new ErrorResult(message),
        AppJsonContext.Default.ErrorResult);
}));

app.MapStaticRoutes(indexHtmlPath, staticDir);
app.MapSkillsRoutes(skillsDir);
app.MapSourcesRoutes();
app.MapSearchRoutes();
app.MapEventsRoutes();
app.MapCodeGraphRoutes();

app.Run();

public partial class Program { }
