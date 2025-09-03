using FitpriseVA.Agents;
using FitpriseVA.Data;
using FitpriseVA.Data.Stores;
using FitpriseVA.Services;
using FitpriseVA.Tools;

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Logging (shows up in VS Output -> Debug) ----------
builder.Logging.ClearProviders();
builder.Logging.AddDebug();
builder.Logging.AddConsole();

// ---------- MVC / Controllers ----------
builder.Services.AddControllers();

// ---------- CORS (dev) ----------
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5290",
                "https://localhost:7290"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FitpriseVA API", Version = "v1" });
    // Force Swagger to use your localhost (adjust if your port differs)
    c.AddServer(new OpenApiServer { Url = "http://localhost:5290" });
});

// ---------- EF Core (SQL Server) ----------
//builder.Services.AddDbContext<AppDbContext>(opt =>
//    opt.UseSqlServer(cfg.GetConnectionString("DefaultConnection"))
//       .EnableSensitiveDataLogging()
//       .EnableDetailedErrors());

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(cfg.GetConnectionString("DefaultConnection"))
       .EnableSensitiveDataLogging()
       .EnableDetailedErrors());



// ---------- Options (tools) ----------
builder.Services.Configure<InternalSearchOptions>(cfg.GetSection("InternalSearch"));
builder.Services.Configure<GoogleSearchOptions>(cfg.GetSection("Google"));

// ---------- HttpClient(s) ----------
builder.Services.AddHttpClient<GoogleSearchTool>();


// ---------- Semantic Kernel + OpenAI Chat ----------
builder.Services.AddOpenAIChatCompletion(
    modelId: cfg["OpenAI:Model"],
    apiKey: cfg["OpenAI:ApiKey"]
);

// Build a Kernel and attach your tools (plugins)
// ---------- Semantic Kernel + OpenAI Chat (already registered above) ----------
// Build a Kernel that uses the existing DI container (no assigning Services)
builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernel = new Kernel(sp);

    // Register only non-circular plugins here.
    // GoogleSearchTool does NOT depend on Kernel, so it's safe:
    kernel.Plugins.AddFromObject(sp.GetRequiredService<GoogleSearchTool>(), "google");

    // IMPORTANT: Do NOT add InternalSearchTool here — it depends (directly/indirectly) on Kernel,
    // which would create a circular dependency and hang DI.
    // You'll add/resolve InternalSearchTool inside your agent at call time if needed.

    return kernel;
});


// ---------- App services (agents, stores, tools) ----------
builder.Services.AddScoped<ConversationStore>();
builder.Services.AddScoped<InternalSearchTool>(); // uses Kernel + SQL
builder.Services.AddScoped<OrchestratorAgent>();  // uses Kernel + IChatCompletionService

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IChatMemory, ChatMemory>();

var app = builder.Build();

// ---------- Dev tools: Swagger UI at /swagger ----------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FitpriseVA v1");
    c.RoutePrefix = "swagger";
});

// ---------- Static files for your minimal React chat (wwwroot/index.html) ----------
app.UseDefaultFiles();
app.UseStaticFiles();

// ---------- CORS (must be before MapControllers) ----------
app.UseCors("DevCors");

// ---------- Map routes ----------
app.MapControllers();

app.Run();
