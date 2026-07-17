using GLORIOUSSYSTEM.Data.Models;

using var db = new HydroponicDbContext();

db.Readings.Add(new Reading
{
    SensorId = 1,
    Timestamp = DateTime.UtcNow.ToString("o"),
    Metric = "pH",
    Value = 6.2
});
db.SaveChanges();

Console.WriteLine("All readings in the database:");
foreach (var r in db.Readings)
{
    Console.WriteLine($"  Sensor {r.SensorId} - {r.Metric}: {r.Value} at {r.Timestamp}");
}