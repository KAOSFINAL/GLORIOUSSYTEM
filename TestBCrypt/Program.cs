using BC = BCrypt.Net.BCrypt;

var hash = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.PZvO.S";
var password = "password123";

Console.WriteLine($"Hash: {hash}");
Console.WriteLine($"Password: '{password}'");
Console.WriteLine($"Password length: {password.Length}");

var result = BC.Verify(password, hash);
Console.WriteLine($"Verify result: {result}");

// Also test with a fresh hash
var newHash = BC.HashPassword(password, 12);
Console.WriteLine($"New hash: {newHash}");

var verifyNew = BC.Verify(password, newHash);
Console.WriteLine($"Verify new hash: {verifyNew}");