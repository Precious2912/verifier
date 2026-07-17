using CrudSystem.Api.Middleware;
using CrudSystem.Api.Seeder;
using CrudSystem.Application;
using CrudSystem.Infrastructure;
using CrudSystem.Infrastructure.Persistence;

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

if (args.Contains("--seed"))
{
    var count = 80; // Number of rows to seed
    var csvPath = "/Users/precious/Documents/MSc/Thesis/dataset/paysim_c2c_transfers.csv";
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
