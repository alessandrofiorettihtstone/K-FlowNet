using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Scheletro.Class;
using Scheletro.Models;
using Scheletro.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Admin/Logout", "/Admin/Logout.cshtml");

    // Le pagine mantengono gli URL legacy con estensione .cshtml, ma sono
    // raggiungibili anche con il formato canonico senza estensione.
    var extensionlessRoutes = new[]
    {
        "/BarcodeScanner",
        "/StampaCartellino",
        "/WebForm1",
        "/Client/Articoli",
        "/Client/Prelievi",
        "/Client/Cartellini",
        "/Client/Clienti",
        "/Client/Combination",
        "/Client/KanbanBoard",
        "/Client/Login",
        "/Client/Logout",
        "/Client/Printer",
        "/Mobile/Default",
        "/Mobile/LeggiCartellinoMain"
    };

    foreach (var route in extensionlessRoutes)
    {
        options.Conventions.AddPageRoute(route, route);
    }
});

builder.Services.AddDbContext<dbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".KFlow.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services.AddScoped<LoggerDTO>();
builder.Services.AddScoped<KFlowAuthService>();
builder.Services.AddScoped<KanbanService>();
builder.Services.AddScoped<PrinterService>();
builder.Services.AddHttpClient();


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

app.UseSession();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    var isMobileSession = context.User.HasClaim(
        AuthConfiguration.AccessChannelClaimType,
        AuthConfiguration.MobileAccessChannel);

    if (isMobileSession && context.Request.Path.StartsWithSegments("/Client"))
    {
        context.Response.Redirect("/Mobile/Default.cshtml");
        return;
    }

    await next();
});

app.UseAuthorization();

app.MapRazorPages();

app.Run();
