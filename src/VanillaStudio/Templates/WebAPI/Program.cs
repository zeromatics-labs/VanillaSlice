using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {{ProjectName}}.Server.Data;
using {{ProjectName}}.Server.Data.Services;
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
    // Must resolve to the SAME file as the other host - see SqliteConnectionStringResolver.
    options.UseSqlite(SqliteConnectionStringResolver.Resolve(connectionString));
{{/if}}
});

builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

{{#if (eq EmailProvider "Dev")}}
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, DevEmailSender>();
{{/if}}
{{#if (eq EmailProvider "Smtp")}}
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();
{{/if}}
{{#if (eq EmailProvider "SendGrid")}}
builder.Services.AddHttpClient<IEmailSender<ApplicationUser>, SendGridEmailSender>();
{{/if}}

builder.Services.AddAuthorization();

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

    var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
{{#if (eq DatabaseProvider "SqlServer")}}
    dbContext.Database.Migrate();
{{/if}}
{{#unless (eq DatabaseProvider "SqlServer")}}
    // No checked-in EF migration ships for this provider; create the schema directly.
    dbContext.Database.EnsureCreated();
{{/unless}}
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/identity").MapIdentityApi<ApplicationUser>();

app.MapControllers();

app.Run();
