using Autofac;
using Autofac.Extensions.DependencyInjection;
using BusinessLayer.Abstract;
using BusinessLayer.AutoMapper.Profiles;
using BusinessLayer.DependencyResolvers;
using BusinessLayer.Errors;
using BusinessLayer.Extension;
using Core.Extensions;
using CoreDemo;
using CoreDemo.AutoMapper.Profiles;
using CoreDemo.Models;
using CoreDemo.Services;
using CoreLayer.DependancyResolvers;
using CoreLayer.Extensions;
using CoreLayer.Utilities.IoC;
using CoreLayer.Utilities.MailUtilities;
using DataAccessLayer.Concrete;
using DataAccessLayer.DependancyInjection;
using EntityLayer.Concrete;
using FluentValidation.AspNetCore;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<Worker>();

builder.Services.AddRazorPages();

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

builder.Host.ConfigureContainer<ContainerBuilder>(
    builder =>
    {
        builder.RegisterModule(new AutofacBusinessModule());
        builder.RegisterModule(new AutofacValidationModule());
    });

CultureInfo[] supportedCultures = new[] { new CultureInfo("tr"), };

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("tr");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider()
    };
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<Context>()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.SignIn.RequireConfirmedEmail = true;
}).AddEntityFrameworkStores<Context>()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));

builder.Services.AddSignalR();

builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IEnvironmentService, EnvironmentService>();

builder.Services.AddMvc(config =>
{
    var policy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();
    config.Filters.Add(new AuthorizeFilter(policy));

}).AddNewtonsoftJson(opt =>
{
    opt.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
}).AddRazorRuntimeCompilation();

builder.Services.AddFluentValidationClientsideAdapters();

builder.Services.Configure<DataProtectionTokenProviderOptions>(opt =>
opt.TokenLifespan = TimeSpan.FromMinutes(5)
);

builder.Services.AddAuthentication(
    CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(x =>
    {
        x.LoginPath = "/Login/Index";
    }
);

builder.Services.AddSingleton(new WriterCity());

builder.Services.AddAutoMapper(cfg => { }, typeof(BusinessImages), typeof(UIImage), typeof(DBOImages));

builder.Services
    .AddDependencyResolvers(
            builder.Configuration,
            new ICoreModule[] {
               new CoreModule()
            });

builder.Services.IocDataAccessInstall(builder.Configuration);

builder.Services.IocBusinessInstall();

builder.Services.AddHttpContextAccessor();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Login/Index";
    options.LogoutPath = "/Login/Logout";
    options.AccessDeniedPath = new PathString("/Login/AccessDenied");
});


if (builder.Environment.IsProduction())
{
    builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<Context>()
    .AddApplicationInsightsPublisher()
    .AddSqlServer(builder.Configuration.GetConnectionString("SQLServer"));

    builder.Services
        .AddHealthChecksUI()
        .AddSqlServerStorage(builder.Configuration.GetConnectionString("SQLServer"));
}

var app = builder.Build();

app.UseHttpsRedirection();

// Statik dosyalar için cache ayarları
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // 1 yıl cache (31536000 saniye)
        const int durationInSeconds = 31536000;
        ctx.Context.Response.Headers["Cache-Control"] = $"public,max-age={durationInSeconds},immutable";
    }
});

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

if (app.Environment.IsProduction())
{
    app.UseUserDestroyer("/Blog/Index");
    app.UseHsts();
    app.ConfigureCustomExceptionMiddleware();

    app.UseHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    });

    app.UseHealthChecksPrometheusExporter("/my-health-metrics", options => options.ResultStatusCodes[HealthStatus.Unhealthy] = (int)HttpStatusCode.OK);

    app.UseHealthChecksUI(options =>
    {
        options.UIPath = "/health-ui";
    });
}
else
{
    app.UseDeveloperExceptionPage();
}

app.MapRazorPages();

app.MapControllerRoute(
    name: "Admin",
    pattern: "/admin/{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "Admin" },
    constraints: new { area = "Admin" }
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Blog}/{action=Index}/{id?}"
);

app.MapHub<SignalRHub>("/ReceiveNotification");

app.Run();