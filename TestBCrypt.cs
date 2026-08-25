using BCrypt.Net;

var hash = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.PZvO.S";
var password = "password123";

Console.WriteLine($"Hash: {hash}");
Console.WriteLine($"Password: '{password}'");
Console.WriteLine($"Password length: {password.Length}");

var result = BCrypt.Verify(password, hash);
Console.WriteLine($"Verify result: {result}");

// Also test with a fresh hash
var newHash = BCrypt.HashPassword(password, 12);
Console.WriteLine($"New hash: {newHash}");
var verifyNew = BCrypt.Verify(password, newHash);
Console.WriteLine($"Verify new hash: {verifyNew}");