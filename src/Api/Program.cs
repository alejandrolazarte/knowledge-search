using KnowledgeSearch;

var (dbPath, docsDir, skillsDir, indexHtmlPath, staticDir) = AppConfig.Load();
var logPath = Path.ChangeExtension(dbPath, ".log");

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://localhost:5111");
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

builder.Services.AddSingleton(new LogService(logPath));
builder.Services.AddHostedService(sp =>
    new WatcherService(docsDir, dbPath, sp.GetRequiredService<LogService>()));

var app = builder.Build();

app.MapStaticRoutes(indexHtmlPath, staticDir);
app.MapSkillsRoutes(skillsDir);
app.MapSearchRoutes(dbPath, docsDir);
app.MapEventsRoutes();

app.Run();
