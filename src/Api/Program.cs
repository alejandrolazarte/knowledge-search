using KnowledgeSearch;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5111");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var dbPath = Environment.GetEnvironmentVariable("KNOWLEDGE_DB")
    ?? builder.Configuration["KnowledgeDb"]
    ?? Path.GetFullPath("../knowledge.db");

var sourceConfiguration = AppConfiguration.ResolveSources(
    builder.Configuration,
    Environment.GetEnvironmentVariable);
var roots = sourceConfiguration.KnowledgeRoots;

var skillsDir = Environment.GetEnvironmentVariable("SKILLS_DIR")
    ?? builder.Configuration["SkillsDir"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");

var indexHtmlPath = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "index.html"))
    ? Path.Combine(Directory.GetCurrentDirectory(), "index.html")
    : Path.Combine(AppContext.BaseDirectory, "index.html");
var staticDir = Path.GetDirectoryName(indexHtmlPath)!;

var logPath = Path.ChangeExtension(dbPath, ".log");

builder.Services.AddSingleton<ISourceConfigurationService>(_ =>
    new SourceConfigurationService(builder.Configuration, Environment.GetEnvironmentVariable));

var dbService = new DbService(dbPath, roots);
builder.Services.AddSingleton<IDbService>(dbService);
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
        roots,
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

    await context.Response.WriteAsJsonAsync(
        new ErrorResult("Error interno del servidor. Revisá los logs para más detalles."),
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
