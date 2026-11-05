using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

/// <summary>Printable academic transcript. Students only see their own.</summary>
[Authorize(Roles = "Admin,Student")]
public class TranscriptController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public TranscriptController(AppDbContext db, UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    // GET: /Transcript?studentId=5
    public async Task<IActionResult> Index(int? studentId)
    {
        int? id = studentId;
        if (!User.IsInRole("Admin"))
        {
            var userId = _users.GetUserId(User);
            id = await _db.Students.Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync();
        }
        if (id == null) return User.IsInRole("Admin") ? RedirectToAction("Index", "Students") : Forbid();

        var student = await _db.Students
            .Include(s => s.Course!).ThenInclude(c => c.Department!).ThenInclude(d => d.Faculty)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();

        var grades = await _db.Grades
            .Include(g => g.Subject).Include(g => g.Term)
            .Where(g => g.StudentId == id)
            .ToListAsync();

        ViewBag.Student = student;
        return View(Academic.Transcript(grades));
    }
}
