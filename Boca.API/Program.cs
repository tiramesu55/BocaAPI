using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Repository;
using BocaAPI.Services;
using Microsoft.Extensions.Logging.EventLog;
using FluentValidation.AspNetCore;
using System.Reflection;
//create options so the service can run from non windows32 folder
WebApplicationOptions options = new()
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args
};

var builder = WebApplication.CreateBuilder(options);

builder.Host.ConfigureServices(services =>
{
    //services.AddHostedService<Worker>();

    if (OperatingSystem.IsWindows())
    {
        services.Configure<EventLogSettings>(config =>
        {
            if (OperatingSystem.IsWindows())
            {
                config.LogName = "Boce Service";
                config.SourceName = "Boca Service Source";
            }
        });
    }
});
builder.Host.UseWindowsService();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();

builder.Services.AddOptions<Settings>("Folders");


builder.Services.AddScoped<IBocaRepository>(s => new BocaRepository(builder.Configuration["ConnectionStrings:BocaDBConnectionString"]));

builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IBocaService, BocaService>();
builder.Services.Configure<Settings>(builder.Configuration.GetSection("Folders"));
var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();
app.UseRouting();
//app.UseAuthorization();
app.UseEndpoints(ep =>
{
    ep.MapControllers();
});


app.Run();
