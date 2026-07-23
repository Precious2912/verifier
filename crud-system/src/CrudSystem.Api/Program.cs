using CrudSystem.Api;
using CrudSystem.Api.Middleware;
using CrudSystem.Api.SeedData;
using CrudSystem.Api.Simulation;
using CrudSystem.Application;
using CrudSystem.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Seed database from CSV if --seed is passed
if (Args.Has(args, "--seed"))
{
    await Seeder.RunAsync(app.Services, builder.Configuration, app.Environment.ContentRootPath);
    return;
}

//Concurrent requests to POST /api/transactions
if (Args.Has(args, "--simulate-activity"))
{
    var intervalMs = Args.GetInt(args, "--interval", 1000);
    await TransactionSimulator.RunAsync(app.Services, intervalMs);
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
