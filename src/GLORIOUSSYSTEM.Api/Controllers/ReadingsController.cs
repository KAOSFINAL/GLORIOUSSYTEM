using GLORIOUSSYSTEM.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLORIOUSSYSTEM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReadingsController : ControllerBase
{
    const int DefaultHistoryLimit = 1000;
    const int MaxHistoryLimit = 5000;

    private readonly HydroponicDbContext _db;

    public ReadingsController(HydroponicDbContext db)
    {
        _db = db;
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
    {
        var result = await _db.Sensors
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Type,
                s.Model,
                s.MinThreshold,
                s.MaxThreshold,
                LatestValue = s.Readings
                    .OrderByDescending(r => r.Timestamp)
                    .Select(r => (double?)r.Value)
                    .FirstOrDefault(),
                LatestMetric = s.Readings
                    .OrderByDescending(r => r.Timestamp)
                    .Select(r => r.Metric)
                    .FirstOrDefault(),
                LatestTimestamp = s.Readings
                    .OrderByDescending(r => r.Timestamp)
                    .Select(r => (DateTime?)r.Timestamp)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpGet("{sensorId}/history")]
    public async Task<IActionResult> GetHistory(
        int sensorId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (sensorId <= 0)
            return BadRequest("sensorId must be greater than zero.");

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest("from must be earlier than or equal to to.");

        var requestedLimit = limit ?? DefaultHistoryLimit;
        if (requestedLimit < 1)
            return BadRequest("limit must be greater than zero.");

        var query = _db.Readings
            .AsNoTracking()
            .Where(r => r.SensorId == sensorId);

        if (from.HasValue)
            query = query.Where(r => r.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.Timestamp <= to.Value);

        var readings = await query
            .OrderByDescending(r => r.Timestamp)
            .Take(Math.Min(requestedLimit, MaxHistoryLimit))
            .Select(r => new { r.Id, r.Timestamp, r.Metric, r.Value })
            .ToListAsync(cancellationToken);

        // Keep the endpoint's existing oldest-to-newest response order after taking the newest window.
        readings.Reverse();

        return Ok(readings);
    }
}
