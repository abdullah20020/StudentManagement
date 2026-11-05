using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using StudentManagement.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();

builder.Services.AddControllersWithViews(options =>
    {
        // Every page needs a signed-in user unless it says [AllowAnonymous].
        options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

        // Labels and validation messages in English or Arabic.
        options.ModelMetadataDetailsProviders.Add(new LocalizedMetadataProvider());
        LocalizedMetadataProvider.LocalizeBindingMessages(options);
    })
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, _) => new UiStringLocalizer());

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<ScheduleService>();

var app = builder.Build();

// Create the database and seed sample data
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Interface language: English (default) or Arabic, chosen with the language switcher (cookie).
// Formatting/parsing culture stays en-US in both, so numbers and dates in forms bind the same way.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(L.English.Name),
    SupportedCultures = new[] { L.English },
    SupportedUICultures = new[] { L.English, L.Arabic },
    RequestCultureProviders = new List<IRequestCultureProvider> { new CookieRequestCultureProvider() },
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
