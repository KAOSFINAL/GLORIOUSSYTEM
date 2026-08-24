using GLORIOUSSYSTEM.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLORIOUSSYSTEM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReadingsController : ControllerBase
{
    private readonly HydroponicDbContext _db;

    public ReadingsController(HydroponicDbContext db)
    {
        _db = db;
    }

    [HttpGet("latest")]
    public IActionResult GetLatest()
    {
        var sensors = _db.Sensors.Include(s => s.Readings).ToList();

        var result = sensors.Select(s =>
        {
            var latest = s.Readings.OrderByDescending(r => r.Timestamp).FirstOrDefault();
            return new
            {
                s.Id,
                s.Name,
                s.Type,
                s.Model,
                s.MinThreshold,
                s.MaxThreshold,
                LatestValue = latest?.Value,
                LatestMetric = latest?.Metric,
                LatestTimestamp = latest?.Timestamp
            };
        });

        return Ok(result);
    }

    [HttpGet("{sensorId}/history")]
    public IActionResult GetHistory(int sensorId)
    {
        var readings = _db.Readings
            .Where(r => r.SensorId == sensorId)
            .OrderBy(r => r.Timestamp)
            .Select(r => new { r.Id, r.Timestamp, r.Metric, r.Value })
            .ToList();

        return Ok(readings);
    }
}