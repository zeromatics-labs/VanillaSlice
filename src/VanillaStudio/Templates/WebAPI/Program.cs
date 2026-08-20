using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using {{ProjectName}}.Server.Data;
using {{ProjectName}}.Server.DataServices.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
{
{{#if (eq DatabaseProvider "SqlServer")}}
    options.UseSqlServer(connectionString);
{{/if}}
{{#if (eq DatabaseProvider "PostgreSQL")}}
    options.UseNpgsql(connectionString);
{{/if}}
{{#if (eq DatabaseProvider "SQLite")}}
    options.UseSqlite(connectionString);
{{/if}}
});

builder.Services.AddServerSideFeatureServices();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers()
    .PartManager.ApplicationParts.Add(new AssemblyPart(typeof({{ProjectName}}.Server.DataServices.Extensions.FeaturesRegistrationExt).Assembly));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseMiddleware<ErrorHandlerMiddleware>();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
