using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;

    public HomeController(AppDbContext db, RegistrationService registration)
    {
        _db = db;
        _registration = registration;
    }

    /// <summary>Sends each role to its own home page; admins get the dashboard.</summary>
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Instructor")) return RedirectToAction("Index", "InstructorPortal");
        if (User.IsInRole("Student")) return RedirectToAction("Index", "StudentPortal");
        if (!User.IsInRole("Admin")) return RedirectToAction("AccessDenied", "Account");

        var term = await _registration.CurrentTermAsync();
        var tid = term?.Id ?? 0;

        // Probation is judged on all grades so far.
        var grades = await _db.Grades.Include(g => g.Subject).Include(g => g.Term).ToListAsync();
        var cgpas = grades.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => Academic.Cgpa(g));
        var probationIds = cgpas.Where(c => c.Value < Academic.ProbationBelow).Select(c => c.Key).ToList();

        var sections = await _db.Sections.Include(s => s.Meetings).Where(s => s.TermId == tid).ToListAsync();
        var activeStudents = await _db.Students.Where(s => s.IsActive).Select(s => s.Id).ToListAsync();
        var registered = await _db.Registrations.Where(r => r.Section!.TermId == tid).Select(r => r.StudentId).Distinct().ToListAsync();

        // Outstanding fees: each student's latest payment with a balance left.
        var latestPayments = await _db.FeePayments.Include(f => f.Student).ToListAsync();
        var owing = latestPayments.GroupBy(f => f.StudentId)
            .Select(g => g.OrderByDescending(f => f.PaidOn).ThenByDescending(f => f.Id).First())
            .Where(f => f.Balance > 0).ToList();

        ViewBag.Term = term;
        ViewBag.Counts = new Dictionary<string, int>
        {
            ["Faculties"] = await _db.Faculties.CountAsync(),
            ["Departments"] = await _db.Departments.CountAsync(),
            ["Programs"] = await _db.Courses.CountAsync(),
            ["Instructors"] = await _db.Instructors.CountAsync(i => i.IsActive),
            ["Students"] = activeStudents.Count,
            ["Rooms"] = await _db.Rooms.CountAsync(),
        };
        ViewBag.SectionCount = sections.Count;
        ViewBag.Unscheduled = sections.Count(s => !s.Meetings.Any());
        ViewBag.NoInstructor = sections.Count(s => s.InstructorId == null);
        ViewBag.Registrations = await _db.Registrations.CountAsync(r => r.Section!.TermId == tid);
        ViewBag.NotRegistered = await _db.Students.Where(s => s.IsActive && !registered.Contains(s.Id))
            .OrderBy(s => s.LastName).ToListAsync();
        ViewBag.Probation = await _db.Students.Include(s => s.Course).Where(s => probationIds.Contains(s.Id)).ToListAsync();
        ViewBag.Cgpas = cgpas;
        ViewBag.Owing = owing;
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
