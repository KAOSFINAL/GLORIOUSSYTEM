using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class Reading
{
    public int Id { get; set; }

    public int SensorId { get; set; }

    public string Timestamp { get; set; } = null!;

    public string Metric { get; set; } = null!;

    public double Value { get; set; }

    public virtual Sensor Sensor { get; set; } = null!;
}
