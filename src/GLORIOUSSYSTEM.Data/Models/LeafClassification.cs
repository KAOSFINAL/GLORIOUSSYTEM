using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class LeafClassification
{
    public int Id { get; set; }

    public int CameraId { get; set; }

    public int? PipeId { get; set; }

    public DateTime Timestamp { get; set; }

    public string? ImagePath { get; set; }

    public string PredictedClass { get; set; } = null!;

    public double Confidence { get; set; }

    public virtual Camera Camera { get; set; } = null!;

    public virtual Pipe? Pipe { get; set; }
}
