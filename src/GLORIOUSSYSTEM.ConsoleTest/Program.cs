using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;

using var db = new HydroponicDbContext();

// Ensure database is created and seeded
db.Database.EnsureCreated();

// Seed nodes
if (!db.Nodes.Any())
{
    db.Nodes.AddRange(
        new Node { Id = 1, Name = "ESP32-S3 Node 1", Type = "ESP32-S3", Description = "Water quality sensors" },
        new Node { Id = 2, Name = "ESP32-S3 Node 2", Type = "ESP32-S3", Description = "Environmental sensors" }
    );
}

// Seed pipes
if (!db.Pipes.Any())
{
    db.Pipes.AddRange(
        new Pipe { Id = 1, PipeNumber = 1, Description = "NFT pipe 1" },
        new Pipe { Id = 2, PipeNumber = 2, Description = "NFT pipe 2" },
        new Pipe { Id = 3, PipeNumber = 3, Description = "NFT pipe 3" },
        new Pipe { Id = 4, PipeNumber = 4, Description = "NFT pipe 4" }
    );
}

// Seed cameras
if (!db.Cameras.Any())
{
    db.Cameras.AddRange(
        new Camera { Id = 1, Name = "Camera 1", Angle = "Angle A" },
        new Camera { Id = 2, Name = "Camera 2", Angle = "Angle B" }
    );
}

// Seed sensors - water quality (Node 1)
if (!db.Sensors.Any(s => s.Type == "pH"))
{
    db.Sensors.AddRange(
        new Sensor { NodeId = 1, Name = "Reservoir pH (BNC)", Type = "pH", Model = "PH-4502C", Notes = "Analog, reservoir, E201-BNC electrode" },
        new Sensor { NodeId = 1, Name = "Channel pH (Gravity)", Type = "pH", Model = "PH-4502C", Notes = "Analog, NFT channel, Gravity module" },
        new Sensor { NodeId = 1, Name = "Reservoir TDS", Type = "TDS", Model = "DFR0300", Notes = "Analog, reservoir, Gravity module" },
        new Sensor { NodeId = 1, Name = "Water Temperature", Type = "WaterTemp", Model = "DS18B20", Notes = "1-Wire, reservoir" },
        new Sensor { NodeId = 1, Name = "Reservoir Level", Type = "UltrasonicLevel", Model = "JSN-SR04T", Notes = "Waterproof, reservoir" }
    );
}

// Seed sensors - environmental (Node 2) - 1x BME280
if (!db.Sensors.Any(s => s.Type == "BME280"))
{
    db.Sensors.Add(new Sensor { NodeId = 2, Name = "BME280 #1", Type = "BME280", Model = "BME280", PositionIndex = 1 });
}

// Seed sensors - flow (Node 2) - single unit on main supply
if (!db.Sensors.Any(s => s.Type == "FlowRate"))
{
    db.Sensors.Add(new Sensor { NodeId = 2, Name = "Flow Main Supply", Type = "FlowRate", Model = "YF-S201" });
}

// Seed display
if (!db.Displays.Any())
{
    db.Displays.Add(new Display { NodeId = 2, Name = "Main TFT Touch", Type = "TFT", Model = "4inch TFT Touch", Width = 480, Height = 320, TouchEnabled = 1 });
}

// Seed admin user
if (!db.Users.Any())
{
    db.Users.Add(new User
    {
        Name = "Admin",
        Email = "admin@glorious.com",
        PasswordHash = "$2a$12$S7euzzUK2Xdhsuu7bvsQpuZ2PkMahY6ULQ6IgfqaB2nn3gpeD5bUa", // BCrypt hash of 'password123' with cost 12
        IsActive = 1,
        CreatedAt = DateTime.UtcNow,
        Role = "Admin"
    });
}
else
{
    // Update existing admin user with correct password hash
    var admin = await db.Users.FirstOrDefaultAsync<User>(u => u.Email == "admin@glorious.com");
    if (admin != null && admin.PasswordHash != "$2a$12$S7euzzUK2Xdhsuu7bvsQpuZ2PkMahY6ULQ6IgfqaB2nn3gpeD5bUa")
    {
        admin.PasswordHash = "$2a$12$S7euzzUK2Xdhsuu7bvsQpuZ2PkMahY6ULQ6IgfqaB2nn3gpeD5bUa";
        Console.WriteLine("Updated admin user password hash");
    }
}

await db.SaveChangesAsync();

// Add sample readings for all sensors that don't have any
var sensors = db.Sensors.ToList();
var now = DateTime.UtcNow;

foreach (var sensor in sensors)
{
    var existingReadings = db.Readings.Where(r => r.SensorId == sensor.Id).Count();
    if (existingReadings > 0) continue; // Skip if already has data

    // Add 3 readings over the last few hours
    for (int i = 2; i >= 0; i--)
    {
        var timestamp = now.AddHours(-i * 1);
        double value = sensor.Type switch
        {
            "pH" => 6.0 + (Random.Shared.NextDouble() * 1.0), // 6.0-7.0
            "TDS" => 800 + (Random.Shared.NextDouble() * 100), // 800-900
            "WaterTemp" => 20 + (Random.Shared.NextDouble() * 5), // 20-25
            "UltrasonicLevel" => 40 + (Random.Shared.NextDouble() * 10), // 40-50
            "BME280" => i == 0 ? 1013 + (Random.Shared.NextDouble() * 10) : // Pressure
                        i == 1 ? 22 + (Random.Shared.NextDouble() * 5) : // Temp
                        14000 + (Random.Shared.NextDouble() * 3000), // Lux
            "FlowRate" => 2.0 + (Random.Shared.NextDouble() * 0.5), // 2.0-2.5
            _ => 0
        };

        string metric = sensor.Type switch
        {
            "pH" => "pH",
            "TDS" => "PPM",
            "WaterTemp" => "Celsius",
            "UltrasonicLevel" => "cm",
            "BME280" => i == 0 ? "hPa" : (i == 1 ? "Celsius" : "Lux"),
            "FlowRate" => "LPerMin",
            _ => ""
        };

        db.Readings.Add(new Reading
        {
            SensorId = sensor.Id,
            Timestamp = timestamp,
            Metric = metric,
            Value = Math.Round(value, 2)
        });
    }
}

await db.SaveChangesAsync();

Console.WriteLine("All sensors in the database:");
foreach (var s in db.Sensors.OrderBy(s => s.Id))
{
    Console.WriteLine($"  {s.Id}: {s.Name} ({s.Type}) - Model: {s.Model}, Node: {s.NodeId}");
}

Console.WriteLine("\nAll readings in the database:");
foreach (var r in db.Readings.OrderBy(r => r.SensorId).ThenBy(r => r.Timestamp))
{
    Console.WriteLine($"  Sensor {r.SensorId} - {r.Metric}: {r.Value} at {r.Timestamp:o}");
}

// Check Users table
Console.WriteLine("\nAll users in the database:");
foreach (var u in db.Users.OrderBy(u => u.Id))
{
    Console.WriteLine($"  {u.Id}: {u.Name} ({u.Email}) - Active: {u.IsActive}, Role: {u.Role}");
    Console.WriteLine($"      PasswordHash: {u.PasswordHash}");
}

