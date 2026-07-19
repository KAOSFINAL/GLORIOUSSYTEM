-- ==========================================================
-- Solar-Powered Hydroponic Lettuce Monitoring System
-- Database schema (SQLite)
-- ==========================================================

-- Physical controllers (ESP32-S3 nodes, Pi 5 added later)
CREATE TABLE Nodes (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,            -- 'ESP32-S3', 'RaspberryPi5'
    Description TEXT
);

-- The 4 NFT pipes, each holding lettuce in cups
CREATE TABLE Pipes (
    Id INTEGER PRIMARY KEY,
    PipeNumber INTEGER NOT NULL UNIQUE,
    Description TEXT
);

-- All sensors, one row per physical unit
CREATE TABLE Sensors (
    Id INTEGER PRIMARY KEY,
    NodeId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,            -- 'pH','TDS','WaterTemp','UltrasonicLevel','BME280','BH1750','FlowRate'
    Model TEXT,                    -- 'PH-4502C','SEN0244','DS18B20','JSN-SR04T','BME280','BH1750','YF-S201'
    PipeId INTEGER,                -- set for flow sensors (one per pipe)
    PositionIndex INTEGER,         -- set for BME280/BH1750 arrays, tracks physical placement
    Notes TEXT,
    Enabled INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (NodeId) REFERENCES Nodes(Id),
    FOREIGN KEY (PipeId) REFERENCES Pipes(Id)
);

-- Time-series sensor readings
CREATE TABLE Readings (
    Id INTEGER PRIMARY KEY,
    SensorId INTEGER NOT NULL,
    Timestamp TEXT NOT NULL,       -- ISO8601
    Metric TEXT NOT NULL,          -- 'pH','PPM','Celsius','cm','hPa','Lux','LPerMin'
    Value REAL NOT NULL,
    FOREIGN KEY (SensorId) REFERENCES Sensors(Id)
);
CREATE INDEX idx_readings_sensor_time ON Readings(SensorId, Timestamp);

-- Cameras (2 angles on the one zone)
CREATE TABLE Cameras (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Angle TEXT,                    -- e.g. 'Left, 45 deg', 'Top-down'
    Model TEXT DEFAULT 'Logitech C920'
);

-- CNN leaf classification results
CREATE TABLE LeafClassifications (
    Id INTEGER PRIMARY KEY,
    CameraId INTEGER NOT NULL,
    PipeId INTEGER,                -- which pipe/cup this result belongs to, if known
    Timestamp TEXT NOT NULL,
    ImagePath TEXT,
    PredictedClass TEXT NOT NULL,  -- e.g. 'Healthy','NitrogenDeficiency','Diseased'
    Confidence REAL NOT NULL,
    FOREIGN KEY (CameraId) REFERENCES Cameras(Id),
    FOREIGN KEY (PipeId) REFERENCES Pipes(Id)
);

-- Actuators, wired up once the Pi/control logic is added
CREATE TABLE Actuators (
    Id INTEGER PRIMARY KEY,
    NodeId INTEGER NOT NULL,
    Name TEXT NOT NULL,            -- 'Nutrient Pump','Grow Light'
    Type TEXT NOT NULL,
    Pin TEXT,
    FOREIGN KEY (NodeId) REFERENCES Nodes(Id)
);

CREATE TABLE ActuatorEvents (
    Id INTEGER PRIMARY KEY,
    ActuatorId INTEGER NOT NULL,
    Timestamp TEXT NOT NULL,
    Action TEXT NOT NULL,          -- 'ON','OFF'
    Reason TEXT,
    FOREIGN KEY (ActuatorId) REFERENCES Actuators(Id)
);

-- ==========================================================
-- Seed data based on your component list
-- ==========================================================

INSERT INTO Nodes (Id, Name, Type, Description) VALUES
    (1, 'ESP32-S3 Node 1', 'ESP32-S3', 'Water quality sensors'),
    (2, 'ESP32-S3 Node 2', 'ESP32-S3', 'Environmental sensors');

INSERT INTO Pipes (Id, PipeNumber, Description) VALUES
    (1, 1, 'NFT pipe 1'),
    (2, 2, 'NFT pipe 2'),
    (3, 3, 'NFT pipe 3'),
    (4, 4, 'NFT pipe 4');

INSERT INTO Cameras (Id, Name, Angle) VALUES
    (1, 'Camera 1', 'Angle A'),
    (2, 'Camera 2', 'Angle B');

-- Water quality sensors (Node 1)
INSERT INTO Sensors (NodeId, Name, Type, Model, Notes) VALUES
    (1, 'Reservoir pH', 'pH', 'PH-4502C', 'Analog, reservoir'),
    (1, 'Reservoir TDS', 'TDS', 'SEN0244', 'Analog, reservoir'),
    (1, 'Water Temperature', 'WaterTemp', 'DS18B20', '1-Wire, reservoir'),
    (1, 'Reservoir Level', 'UltrasonicLevel', 'JSN-SR04T', 'Waterproof, reservoir');

-- Environmental sensors (Node 2) - 4x BME280, 5x BH1750
INSERT INTO Sensors (NodeId, Name, Type, Model, PositionIndex) VALUES
    (2, 'BME280 #1', 'BME280', 'BME280', 1),
    (2, 'BME280 #2', 'BME280', 'BME280', 2),
    (2, 'BH1750 #1', 'BH1750', 'BH1750', 1),

-- Flow sensors, one per pipe
INSERT INTO Sensors (NodeId, Name, Type, Model, PipeId) VALUES
    (2, 'Flow Pipe 1', 'FlowRate', 'YF-S201', 1),
    (2, 'Flow Pipe 2', 'FlowRate', 'YF-S201', 2),
    (2, 'Flow Pipe 3', 'FlowRate', 'YF-S201', 3),
    (2, 'Flow Pipe 4', 'FlowRate', 'YF-S201', 4);
