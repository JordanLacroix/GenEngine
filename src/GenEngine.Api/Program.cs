var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
