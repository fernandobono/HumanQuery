using Microsoft.OpenApi.Models;
using HumanQuery.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS abierto: es un proyecto educativo que se consume desde el navegador y desde un GPT
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HumanQuery",
        Version = "v1",
        Description = "Consultas en lenguaje natural a SQL Server usando un LLM"
    });
});

// Servicios de HumanQuery
builder.Services.AddHttpClient<OpenAiService>();
builder.Services.AddSingleton<QueryLogService>();

var app = builder.Build();

// Páginas de wwwroot (navegador y monitor)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
