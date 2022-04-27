using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Repository;
using BocaAPI.Services;
using Microsoft.Extensions.Logging.EventLog;
using FluentValidation.AspNetCore;
using System.Reflection;
using BocaAPI;
//create options so the service can run from non windows32 folder
WebApplicationOptions options = new()
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args
};

var builder = WebApplication.CreateBuilder(options);

builder.Host.UseWindowsService();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<EventLogSettings>(conf =>
{
    conf.LogName = string.Empty;
    conf.SourceName = "_BocaService";
});

builder.Services.AddOptions<Settings>("Folders");
builder.Services.AddOptions<EmailConfig>("EmailConfiguration");

builder.Services.AddSingleton<IBocaRepository>(s => new BocaRepository(builder.Configuration["ConnectionStrings:BocaDBConnectionString"]));

builder.Services.AddSingleton<IEmail, Email>();
builder.Services.AddScoped<IBocaService, BocaService>();
builder.Services.Configure<Settings>(builder.Configuration.GetSection("Folders"));

builder.Services.Configure<EmailConfig>(builder.Configuration.GetSection("EmailConfiguration"));
builder.Host.ConfigureServices(services =>
{
    services.AddHostedService<Worker>();

});
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
