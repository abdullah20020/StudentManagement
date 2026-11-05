using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class StudentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;
    private readonly UserManager<IdentityUser> _users;

    public StudentsController(AppDbContext db, RegistrationService registration, UserManager<IdentityUser> users)
    {
        _db = db;
        _registration = registration;
        _users = users;
    }

    // GET: /Students
    public async Task<IActionResult> Index(string search, int? courseId)
    {
        var query = _db.Students
            .Include(s => s.Course)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(s =>
                s.FirstName.Contains(search) ||
                s.LastName.Contains(search) ||
                s.AdmissionNumber.Contains(search));

        if (courseId.HasValue)
            query = query.Where(s => s.CourseId == courseId);

        ViewBag.Search = search;
        ViewBag.CourseId = courseId;
        ViewBag.Courses = await _db.Courses.ToListAsync();
        return View(await query.OrderBy(s => s.AdmissionNumber).ToListAsync());
    }

    // GET: /Students/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var student = await _db.Students
            .Include(s => s.Course).ThenInclude(c => c!.Department).ThenInclude(d => d!.Faculty)
            .Include(s => s.Grades).ThenInclude(g => g.Subject)
            .Include(s => s.Grades).ThenInclude(g => g.Term)
            .Include(s => s.FeePayments)
            .Include(s => s.Attendances)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();

        var term = await _registration.CurrentTermAsync();
        var tid = term?.Id ?? 0;
        ViewBag.Term = term;
        ViewBag.Registrations = await _db.Registrations
            .Include(r => r.Section!).ThenInclude(s => s.Subject)
            .Include(r => r.Section!).ThenInclude(s => s.Instructor)
            .Where(r => r.StudentId == id && r.Section!.TermId == tid)
            .ToListAsync();
        ViewBag.HasLogin = student.UserId != null && await _users.FindByIdAsync(student.UserId) != null;
        return View(student);
    }

    // GET: /Students/Create
    public IActionResult Create()
    {
        ViewBag.Courses = new SelectList(_db.Courses, "Id", "Name");
        // Start from a new Student so defaults (IsActive = true, Gender) show in the form.
        return View(new Student { DateOfBirth = DateTime.Today.AddYears(-18) });
    }

    // POST: /Students/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Student student)
    {
        // Generated server-side below, so it isn't part of the form.
        ModelState.Remove(nameof(Student.AdmissionNumber));

        if (!ModelState.IsValid)
        {
            ViewBag.Courses = new SelectList(_db.Courses, "Id", "Name");
            return View(student);
        }

        student.AdmissionNumber = await GenerateAdmissionNumber();
        student.EnrollmentDate = DateTime.Now;
        student.UserId = null;
        _db.Students.Add(student);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Student registered with ID {0}.", student.AdmissionNumber);
        return RedirectToAction(nameof(Details), new { id = student.Id });
    }

    // GET: /Students/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var student = await _db.Students.FindAsync(id);
        if (student == null) return NotFound();
        ViewBag.Courses = new SelectList(_db.Courses, "Id", "Name", student.CourseId);
        return View(student);
    }

    // POST: /Students/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Student student)
    {
        if (id != student.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            ViewBag.Courses = new SelectList(_db.Courses, "Id", "Name", student.CourseId);
            return View(student);
        }

        // The login link isn't part of the form; keep whatever is stored.
        student.UserId = await _db.Students.Where(s => s.Id == id).Select(s => s.UserId).FirstOrDefaultAsync();
        _db.Update(student);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Student updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Students/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var student = await _db.Students.FindAsync(id);
        if (student != null)
        {
            if (student.UserId != null && await _users.FindByIdAsync(student.UserId) is { } user)
                await _users.DeleteAsync(user);
            _db.Students.Remove(student);
            await _db.SaveChangesAsync();
            TempData["Success"] = T("Student deleted.");
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Creates a student login with a one-time password shown to the admin.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLogin(int id)
    {
        var student = await _db.Students.FindAsync(id);
        if (student == null) return NotFound();

        var (userId, message) = await Accounts.CreateLoginAsync(_users, student.Email, SeedData.StudentRole);
        if (userId != null)
        {
            student.UserId = userId;
            await _db.SaveChangesAsync();
            TempData["Success"] = message;
        }
        else TempData["Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Year of entry followed by a 4-digit sequence, e.g. 20260017.</summary>
    private async Task<string> GenerateAdmissionNumber()
    {
        var prefix = DateTime.Now.Year.ToString();
        var numbers = await _db.Students
            .Where(s => s.AdmissionNumber.StartsWith(prefix) && s.AdmissionNumber.Length == 8)
            .Select(s => s.AdmissionNumber)
            .ToListAsync();
        var next = numbers.Select(n => int.TryParse(n[4..], out var x) ? x : 0).DefaultIfEmpty(0).Max() + 1;
        return $"{prefix}{next:D4}";
    }
}
