using BocaAPI.Interfaces;
using BocaAPI.Repository;
using BocaAPI.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Sinks.MSSqlServer;
//using Boca.API.DbContexts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(opt=>
{
    opt.ReturnHttpNotAcceptable = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();


builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

//builder.Services.AddDbContext<RecepieInfoContext>(
//    dbContextOpts => dbContextOpts.UseSqlite(
//          builder.Configuration["ConnectionStrings:CityInfoDBConnectionString"]));

builder.Services.AddScoped<IBocaRepository>(s => new BocaRepository(builder.Configuration["ConnectionStrings:BocaDBConnectionString"]));
builder.Services.AddScoped<ILoggerService, LoggerService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IBocaService, BocaService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseEndpoints(ep =>
{
    ep.MapControllers();
});


app.Run();
