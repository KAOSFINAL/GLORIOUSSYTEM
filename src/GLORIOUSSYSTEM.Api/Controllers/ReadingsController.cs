using GLORIOUSSYSTEM.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLORIOUSSYSTEM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReadingsController : ControllerBase
{

[HttpGet("latest")]
public IActionResult GetLatest()
{
    using var db = new HydroponicDbContext();
    var sensors = db.Sensors.Include(s => s.Readings).ToList();

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
    using var db = new HydroponicDbContext();
    var readings = db.Readings
        .Where(r => r.SensorId == sensorId)
        .OrderBy(r => r.Timestamp)
        .Select(r => new { r.Id, r.Timestamp, r.Metric, r.Value })
        .ToList();

    return Ok(readings);
	}
}