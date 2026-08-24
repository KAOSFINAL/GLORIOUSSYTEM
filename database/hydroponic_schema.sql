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
    Type TEXT NOT NULL,            -- 'pH','TDS','WaterTemp','UltrasonicLevel','BME280','FlowRate'
    Model TEXT,                    -- 'PH-4502C','DFR0300','DS18B20','JSN-SR04T','BME280','YF-S201'
    PipeId INTEGER,                -- set for flow sensors (one per pipe)
    PositionIndex INTEGER,         -- set for BME280 arrays, tracks physical placement
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
    Metric TEXT NOT NULL,          -- 'pH','PPM','Celsius','cm','hPa','Lux','LPerMin','GasResistance','IAQ'
    Value REAL NOT NULL,
    FOREIGN KEY (SensorId) REFERENCES Sensors(Id)
);
CREATE INDEX idx_readings_sensor_time ON Readings(SensorId, Timestamp);

-- Cameras (2 angles on the one zone)
CREATE TABLE Cameras (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Angle TEXT,                    -- e.g. 'Left, 45 deg', 'Top-down'
    Model TEXT DEFAULT 'EMEET C950'
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

-- Display/Output devices (TFT, LEDs, etc.)
CREATE TABLE Displays (
    Id INTEGER PRIMARY KEY,
    NodeId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,            -- 'TFT', 'LCD', 'OLED', 'LEDMatrix'
    Model TEXT,                    -- '4inch TFT Touch', 'SSD1306', etc.
    Width INTEGER,
    Height INTEGER,
    TouchEnabled INTEGER DEFAULT 0,
    FOREIGN KEY (NodeId) REFERENCES Nodes(Id)
);

-- Users for authentication
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    LastLoginAt TEXT,
    Role TEXT DEFAULT 'User'
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
    (1, 'Reservoir pH (BNC)', 'pH', 'PH-4502C', 'Analog, reservoir, E201-BNC electrode'),
    (1, 'Channel pH (Gravity)', 'pH', 'PH-4502C', 'Analog, NFT channel, Gravity module'),
    (1, 'Reservoir TDS', 'TDS', 'DFR0300', 'Analog, reservoir, Gravity module'),
    (1, 'Water Temperature', 'WaterTemp', 'DS18B20', '1-Wire, reservoir'),
    (1, 'Reservoir Level', 'UltrasonicLevel', 'JSN-SR04T', 'Waterproof, reservoir');

-- Environmental sensors (Node 2) - 1x BME280
INSERT INTO Sensors (NodeId, Name, Type, Model, PositionIndex) VALUES
    (2, 'BME280 #1', 'BME280', 'BME280', 1),

-- Flow sensor (single unit on main supply)
INSERT INTO Sensors (NodeId, Name, Type, Model, PipeId) VALUES
    (2, 'Flow Main Supply', 'FlowRate', 'YF-S201', NULL);

-- Display/Output devices
INSERT INTO Displays (NodeId, Name, Type, Model, Width, Height, TouchEnabled) VALUES
    (2, 'Main TFT Touch', 'TFT', '4inch TFT Touch', 480, 320, 1);

-- Default admin user (password: password123)
-- BCrypt hash of 'password123' with cost 12
INSERT INTO Users (Id, Name, Email, PasswordHash, IsActive, Role) VALUES
    (1, 'Admin', 'admin@glorious.com', '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.PZvO.S', 1, 'Admin');
