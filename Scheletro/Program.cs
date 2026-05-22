using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Scheletro.Class;
using Scheletro.Models;
using Scheletro.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Admin/Logout", "/Client/Logout.cshtml");
    options.Conventions.AddPageRoute("/Admin/Logout", "/Admin/Logout.cshtml");
});

builder.Services.AddDbContext<dbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<LoggerDTO>();
builder.Services.AddScoped<KFlowAuthService>();
builder.Services.AddScoped<KanbanService>();
builder.Services.AddScoped<PrinterService>();


builder.Services
    .AddAuthentication(AuthConfiguration.CookieScheme)
    .AddCookie(AuthConfiguration.CookieScheme, options =>
    {
        options.Cookie.Name = "KFlowAuth";
        options.LoginPath = "/Client/Login.cshtml";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Mobile"))
            {
                context.Response.Redirect("/Mobile/Default.cshtml");
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(AuthConfiguration.CookieScheme)
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(AuthConfiguration.AdminPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(AuthConfiguration.CookieScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AuthConfiguration.AdminRole);
    });

    options.AddPolicy(AuthConfiguration.NonAdminPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(AuthConfiguration.CookieScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => !AuthConfiguration.HasAdminRole(context.User));
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
