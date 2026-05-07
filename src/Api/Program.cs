using KnowledgeSearch;

var (dbPath, docsDir, skillsDir, indexHtmlPath, staticDir) = AppConfig.Load();

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://localhost:5111");
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var app = builder.Build();

app.MapStaticRoutes(indexHtmlPath, staticDir);
app.MapSkillsRoutes(skillsDir);
app.MapSearchRoutes(dbPath, docsDir);

app.Run();
