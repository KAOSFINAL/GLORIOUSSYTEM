using Microsoft.EntityFrameworkCore;
using GLORIOUSSYSTEM.Data.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddDbContext<GLORIOUSSYSTEM.Data.Models.HydroponicDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("HydroponicDb")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
    SensorHardwareCatalog.Reconcile(db);
    db.SaveChanges();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
