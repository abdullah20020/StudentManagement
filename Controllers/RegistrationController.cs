using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

/// <summary>
/// Course registration. Students see their own page (and only while registration is open);
/// admins can open any student's page and register them even after registration closes.
/// </summary>
[Authorize(Roles = "Admin,Student")]
public class RegistrationController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;
    private readonly UserManager<IdentityUser> _users;

    public RegistrationController(AppDbContext db, RegistrationService registration, UserManager<IdentityUser> users)
    {
        _db = db;
        _registration = registration;
        _users = users;
    }

    private bool IsAdmin => User.IsInRole("Admin");

    // GET: /Registration?studentId=5&termId=3
    public async Task<IActionResult> Index(int? studentId, int? termId)
    {
        var id = await ResolveStudentId(studentId);
        if (id == null) return IsAdmin ? RedirectToAction("Index", "Students") : Forbid();

        var term = termId.HasValue ? await _db.Terms.FindAsync(termId.Value) : await _registration.CurrentTermAsync();
        if (term == null)
        {
            TempData["Error"] = T("No academic term has been set up yet.");
            return IsAdmin ? RedirectToAction("Index", "Terms") : RedirectToAction("Index", "Home");
        }

        var ctx = await _registration.LoadAsync(id.Value, term.Id);
        if (ctx == null) return NotFound();

        ViewBag.Context = ctx;
        ViewBag.IsAdmin = IsAdmin;
        ViewBag.Terms = await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
        ViewBag.Timetable = ScheduleService.Entries(ctx.Current.Select(r => r.Section!));
        return View(await _registration.OfferingsAsync(ctx, IsAdmin));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int sectionId, int? studentId)
    {
        var id = await ResolveStudentId(studentId);
        if (id == null) return Forbid();

        var (ok, message) = await _registration.RegisterAsync(id.Value, sectionId, IsAdmin);
        TempData[ok ? "Success" : "Error"] = message;
        var termId = await _db.Sections.Where(s => s.Id == sectionId).Select(s => (int?)s.TermId).FirstOrDefaultAsync();
        return RedirectToAction(nameof(Index), new { studentId = IsAdmin ? id : null, termId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Drop(int registrationId, int? studentId)
    {
        var id = await ResolveStudentId(studentId);
        if (id == null) return Forbid();

        var reg = await _db.Registrations.Include(r => r.Section).FirstOrDefaultAsync(r => r.Id == registrationId);
        if (reg == null || reg.StudentId != id) return NotFound();   // students can only drop their own

        var (ok, message) = await _registration.DropAsync(registrationId, IsAdmin);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index), new { studentId = IsAdmin ? id : null, termId = reg.Section!.TermId });
    }

    /// <summary>Admins pick the student; a student always gets their own record.</summary>
    private async Task<int?> ResolveStudentId(int? requested)
    {
        if (IsAdmin) return requested;
        var userId = _users.GetUserId(User);
        return await _db.Students.Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync();
    }
}
