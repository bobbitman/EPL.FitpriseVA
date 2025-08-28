
using FitpriseVA.Agents;
using FitpriseVA.Data;
using FitpriseVA.Data.Stores;
using FitpriseVA.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Logging.ClearProviders();
builder.Logging.AddDebug(); 
builder.Logging.AddConsole();


// 1) EF Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(opt =>
opt.UseSqlServer(cfg.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<ConversationStore>();


// 2) Bind options
builder.Services.Configure<OpenAIOptions>(cfg.GetSection("OpenAI"));
builder.Services.Configure<GoogleSearchOptions>(cfg.GetSection("Google"));


// 3) Semantic Kernel + OpenAI chat + tool registration
builder.Services.AddHttpClient();
builder.Services.AddScoped<GoogleSearchTool>();
builder.Services.AddScoped<InternalSearchTool>();

builder.Services.AddSingleton(sp =>
{
    var openAi = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAIOptions>>().Value;
    var kernelBuilder = Kernel.CreateBuilder();

    // OpenAI chat completion
    kernelBuilder.AddOpenAIChatCompletion(modelId: openAi.Model, apiKey: openAi.ApiKey);

    // Register tools (plugins)
    kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<GoogleSearchTool>(), "google");
    kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<InternalSearchTool>(), "internal");

    return kernelBuilder.Build();
});


builder.Services.AddScoped<AssistantAgent>();
builder.Services.AddScoped<OrchestratorAgent>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = false;
    options.ValidateOnBuild = false;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FitpriseVA v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("DevCors");
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

var dataSource = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
foreach (var e in dataSource.Endpoints)
    Console.WriteLine("[ROUTE] " + e.DisplayName);




app.Run();
