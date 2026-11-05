using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

/// <summary>The signed-in student's own dashboard.</summary>
[Authorize(Roles = "Student")]
public class StudentPortalController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;
    private readonly UserManager<IdentityUser> _users;

    public StudentPortalController(AppDbContext db, RegistrationService registration, UserManager<IdentityUser> users)
    {
        _db = db;
        _registration = registration;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _users.GetUserId(User);
        var student = await _db.Students
            .Include(s => s.Course!).ThenInclude(c => c.Department!).ThenInclude(d => d.Faculty)
            .Include(s => s.FeePayments)
            .Include(s => s.Grades).ThenInclude(g => g.Subject)
            .Include(s => s.Grades).ThenInclude(g => g.Term)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.UserId == userId);
        if (student == null)
            return View("NoProfile");

        var term = await _registration.CurrentTermAsync();
        var registrations = term == null ? new() : await _db.Registrations
            .Include(r => r.Section!).ThenInclude(s => s.Subject)
            .Include(r => r.Section!).ThenInclude(s => s.Instructor)
            .Include(r => r.Section!).ThenInclude(s => s.Meetings).ThenInclude(m => m.Room)
            .Where(r => r.StudentId == student.Id && r.Section!.TermId == term.Id)
            .ToListAsync();

        var subjectIds = registrations.Select(r => r.Section!.SubjectId).ToList();
        var attendance = term == null ? new() : await _db.Attendances
            .Where(a => a.StudentId == student.Id && subjectIds.Contains(a.SubjectId) && a.Date >= term.StartDate && a.Date <= term.EndDate)
            .ToListAsync();

        ViewBag.Term = term;
        ViewBag.Registrations = registrations;
        ViewBag.Attendance = attendance;
        ViewBag.Transcript = Academic.Transcript(student.Grades);
        ViewBag.Timetable = ScheduleService.Entries(registrations.Select(r => r.Section!));
        return View(student);
    }
}
