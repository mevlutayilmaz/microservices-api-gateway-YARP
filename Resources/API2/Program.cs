var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api2", () => "API 2 - " + args[0]);

app.Run();
