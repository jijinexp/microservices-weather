using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PrecipDbContext>(
    opts => {
        opts.EnableSensitiveDataLogging();
        opts.EnableDetailedErrors();
        opts.UseNpgsql(builder.Configuration.GetConnectionString("AppDb"));
    }, ServiceLifetime.Transient
);
var app = builder.Build();

app.MapGet("/observation/{zip}",async (string zip, [FromQuery] int? days, PrecipDbContext db) => {
    if(days is null || days < 1 || days > 30){
        return Results.BadRequest("Please provide a 'days' query parameter betweeen 1 and 30");
    }
    var startData = DateTime.UtcNow - TimeSpan.FromDays(days.Value);
    var results = await db.Precipitation
    .Where(precip => precip.ZipCode == zip && precip.CreatedOn > startData)
    .ToListAsync();
    return Results.Ok(results);
});

app.Run();