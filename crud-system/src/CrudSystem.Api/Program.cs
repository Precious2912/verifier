using CrudSystem.Api.Middleware;
using CrudSystem.Api.Seeder;
using CrudSystem.Application;
using CrudSystem.Infrastructure;
using CrudSystem.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var config = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (args.Contains("--seed"))
{
    var count = config.GetValue<int?>("DataSetConfig:RecordCount")
                  ?? throw new InvalidOperationException("RecordCount is missing!");
    if (count <= 0)
        throw new InvalidOperationException($"RecordCount must be positive, got {count}.");

    var csvPath = config.GetValue<string>("DataSetConfig:CSVPath")
                  ?? throw new InvalidOperationException("CSVPath is missing!");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await PaySimSeeder.SeedAsync(db, csvPath, count);
    return;
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
