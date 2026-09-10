using Microsoft.EntityFrameworkCore;

namespace GLORIOUSSYSTEM.Data.Models;

/// <summary>
/// Keeps persisted sensor records aligned with the physical GLORIOUS SYSTEM hardware.
/// The reconciliation is idempotent so it is safe for both packaged and upgraded databases.
/// </summary>
public static class SensorHardwareCatalog
{
    public static void Reconcile(HydroponicDbContext db)
    {
        var nodeIds = db.Nodes
            .AsNoTracking()
            .OrderBy(node => node.Id)
            .Select(node => node.Id)
            .ToList();

        if (nodeIds.Count == 0)
            return;

        var waterNodeId = nodeIds[0];
        var environmentNodeId = nodeIds.Count > 1 ? nodeIds[1] : waterNodeId;
        var sensors = db.Sensors
            .Include(sensor => sensor.Readings)
            .OrderBy(sensor => sensor.Id)
            .ToList();

        Sensor AddSensor(int nodeId, string name, string type, string model, string notes)
        {
            var sensor = new Sensor
            {
                NodeId = nodeId,
                Name = name,
                Type = type,
                Model = model,
                Notes = notes,
                Enabled = 1
            };
            db.Sensors.Add(sensor);
            sensors.Add(sensor);
            return sensor;
        }

        static bool IsType(Sensor sensor, string type)
            => string.Equals(sensor.Type, type, StringComparison.OrdinalIgnoreCase);

        var phSensors = sensors.Where(sensor => IsType(sensor, "pH")).ToList();
        var reservoirPh = phSensors.FirstOrDefault(sensor =>
                sensor.Name.Contains("Reservoir", StringComparison.OrdinalIgnoreCase) ||
                (sensor.Notes?.Contains("E201", StringComparison.OrdinalIgnoreCase) ?? false))
            ?? phSensors.FirstOrDefault()
            ?? AddSensor(waterNodeId, "Reservoir pH", "pH", "PH-450C / E201-BNC",
                "Analog reservoir pH sensor with E201-BNC electrode");

        reservoirPh.NodeId = waterNodeId;
        reservoirPh.Name = "Reservoir pH";
        reservoirPh.Type = "pH";
        reservoirPh.Model = "PH-450C / E201-BNC";
        reservoirPh.Notes = "Analog reservoir pH sensor with E201-BNC electrode";

        foreach (var duplicate in phSensors.Where(sensor => sensor != reservoirPh))
        {
            db.Readings.RemoveRange(duplicate.Readings);
            db.Sensors.Remove(duplicate);
            sensors.Remove(duplicate);
        }

        var tds = sensors.FirstOrDefault(sensor => IsType(sensor, "TDS"))
            ?? AddSensor(waterNodeId, "Nutrient TDS", "TDS", "DFR0300",
                "Analog nutrient concentration sensor in the reservoir");
        tds.NodeId = waterNodeId;
        tds.Name = "Nutrient TDS";
        tds.Type = "TDS";
        tds.Model = "DFR0300";

        var waterTemperature = sensors.FirstOrDefault(sensor => IsType(sensor, "WaterTemp"))
            ?? AddSensor(waterNodeId, "Water Temperature", "WaterTemp", "DS18B20", "1-Wire reservoir probe");
        waterTemperature.NodeId = waterNodeId;
        waterTemperature.Name = "Water Temperature";
        waterTemperature.Type = "WaterTemp";
        waterTemperature.Model = "DS18B20";

        var waterLevel = sensors.FirstOrDefault(sensor => IsType(sensor, "UltrasonicLevel"))
            ?? AddSensor(waterNodeId, "Water Level", "UltrasonicLevel", "JSN-SR04T", "Waterproof reservoir level sensor");
        waterLevel.NodeId = waterNodeId;
        waterLevel.Name = "Water Level";
        waterLevel.Type = "UltrasonicLevel";
        waterLevel.Model = "JSN-SR04T";

        var environmentSensors = sensors.Where(sensor =>
                sensor.Type.StartsWith("BME", StringComparison.OrdinalIgnoreCase) ||
                (sensor.Model?.StartsWith("BME", StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        var environment = environmentSensors.FirstOrDefault()
            ?? AddSensor(environmentNodeId, "Environment", "BME680", "BME680",
                "Temperature, humidity, pressure, and air-quality sensor");
        environment.NodeId = environmentNodeId;
        environment.Name = "Environment";
        environment.Type = "BME680";
        environment.Model = "BME680";
        environment.PositionIndex = 1;
        environment.Notes = "Temperature, humidity, pressure, and air-quality sensor";

        foreach (var duplicate in environmentSensors.Where(sensor => sensor != environment))
        {
            foreach (var reading in duplicate.Readings)
                reading.Sensor = environment;
            db.Sensors.Remove(duplicate);
            sensors.Remove(duplicate);
        }

        var light = sensors.FirstOrDefault(sensor =>
                IsType(sensor, "BH1750") ||
                string.Equals(sensor.Model, "BH1750", StringComparison.OrdinalIgnoreCase))
            ?? AddSensor(environmentNodeId, "Light Intensity", "BH1750", "BH1750", "Digital ambient light sensor");
        light.NodeId = environmentNodeId;
        light.Name = "Light Intensity";
        light.Type = "BH1750";
        light.Model = "BH1750";

        foreach (var reading in environment.Readings.Where(reading =>
                     string.Equals(reading.Metric, "Lux", StringComparison.OrdinalIgnoreCase)))
            reading.Sensor = light;

        var flowSensors = sensors.Where(sensor => IsType(sensor, "FlowRate")).ToList();
        if (flowSensors.Count == 0)
        {
            flowSensors.Add(AddSensor(environmentNodeId, "Flow Main Supply", "FlowRate", "YF-S201",
                "Main nutrient supply flow sensor"));
        }

        foreach (var flow in flowSensors)
        {
            flow.Type = "FlowRate";
            flow.Model = "YF-S201";
        }
    }
}
