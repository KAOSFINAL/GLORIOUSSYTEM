using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class HydroponicDbContext : DbContext
{
    public HydroponicDbContext()
    {
    }

    public HydroponicDbContext(DbContextOptions<HydroponicDbContext> options)
        : base(options)
    {
    }

    private static string GetConnectionString()
    {
        var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        return config.GetConnectionString("HydroponicDb")
            ?? "Data Source=C:\\Dev\\GLORIOUSSYSTEM\\database\\hydroponic.db";
    }

    public virtual DbSet<Actuator> Actuators { get; set; }

    public virtual DbSet<ActuatorEvent> ActuatorEvents { get; set; }

    public virtual DbSet<Camera> Cameras { get; set; }

    public virtual DbSet<Display> Displays { get; set; }

    public virtual DbSet<LeafClassification> LeafClassifications { get; set; }

    public virtual DbSet<Node> Nodes { get; set; }

    public virtual DbSet<Pipe> Pipes { get; set; }

    public virtual DbSet<Reading> Readings { get; set; }

    public virtual DbSet<Sensor> Sensors { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = GetConnectionString();
            optionsBuilder.UseSqlite(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Actuator>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasOne(d => d.Node).WithMany(p => p.Actuators)
                .HasForeignKey(d => d.NodeId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ActuatorEvent>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasOne(d => d.Actuator).WithMany(p => p.ActuatorEvents)
                .HasForeignKey(d => d.ActuatorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Camera>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Model).HasDefaultValue("EMEET C950");
        });

        modelBuilder.Entity<Display>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasOne(d => d.Node).WithMany(p => p.Displays)
                .HasForeignKey(d => d.NodeId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<LeafClassification>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasOne(d => d.Camera).WithMany(p => p.LeafClassifications)
                .HasForeignKey(d => d.CameraId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Pipe).WithMany(p => p.LeafClassifications).HasForeignKey(d => d.PipeId);
        });

        modelBuilder.Entity<Node>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Pipe>(entity =>
        {
            entity.HasIndex(e => e.PipeNumber, "IX_Pipes_PipeNumber").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Reading>(entity =>
        {
            entity.HasIndex(e => new { e.SensorId, e.Timestamp }, "idx_readings_sensor_time");

            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasOne(d => d.Sensor).WithMany(p => p.Readings)
                .HasForeignKey(d => d.SensorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Enabled).HasDefaultValue(1);

            entity.HasOne(d => d.Node).WithMany(p => p.Sensors)
                .HasForeignKey(d => d.NodeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Pipe).WithMany(p => p.Sensors).HasForeignKey(d => d.PipeId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
