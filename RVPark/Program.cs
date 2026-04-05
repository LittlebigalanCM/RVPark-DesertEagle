using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Stripe;
using static Infrastructure.Services.TransactionService;


var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
#pragma warning disable CS0436 // Type conflicts with imported type
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.MigrationsAssembly("Infrastructure")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
//test

//takes care of all users accounts and logins
//Identity framework! requires entity framework (database)
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.AddSingleton<IEmailSender, EmailSender>();

//Builds razor pages
builder.Services.AddRazorPages();

builder.Services.AddScoped<UnitOfWork>();
builder.Services.AddScoped<DbInitializer>();
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

//add service for checking reservation status
builder.Services.AddHostedService<ReservationStatusChecker>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<TransactionAuditService>();


var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
StripeConfiguration.ApiKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();


app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
SeedDatabase();
void SeedDatabase()
{
    using var scope = app.Services.CreateScope();
    var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    dbInitializer.Initialize();
}

app.Run();
