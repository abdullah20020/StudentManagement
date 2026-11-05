using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.Localization;

namespace StudentManagement.Controllers;

/// <summary>Switches the interface between English and Arabic (stored in a cookie for a year).</summary>
[AllowAnonymous]
public class LanguageController : Controller
{
    // GET: /Language/Set?lang=ar&returnUrl=/Students
    public IActionResult Set(string lang, string? returnUrl)
    {
        // Number and date parsing always stay en-US, so form values like "12.5" bind the same in both languages;
        // only the interface language (UI culture) changes.
        var ui = lang == "ar" ? L.Arabic.Name : L.English.Name;
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(L.English.Name, ui)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    /// <summary>Development aid: English text shown in Arabic mode that has no translation yet.</summary>
    public IActionResult Missing([FromServices] IWebHostEnvironment env)
    {
        if (!env.IsDevelopment()) return NotFound();
        return Content(string.Join("\n", L.Missing.Keys.OrderBy(k => k)), "text/plain; charset=utf-8");
    }
}
