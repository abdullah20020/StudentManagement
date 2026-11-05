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
public class InstructorsController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;
    private readonly UserManager<IdentityUser> _users;

    public InstructorsController(AppDbContext db, RegistrationService registration, UserManager<IdentityUser> users)
    {
        _db = db;
        _registration = registration;
        _users = users;
    }

    public async Task<IActionResult> Index(int? departmentId, string? search)
    {
        var term = await _registration.CurrentTermAsync();
        var termId = term?.Id ?? 0;
        var query = _db.Instructors
            .Include(i => i.Department!).ThenInclude(d => d.Faculty)
            .Include(i => i.Sections.Where(s => s.TermId == termId)).ThenInclude(s => s.Subject)
            .AsQueryable();

        if (departmentId.HasValue) query = query.Where(i => i.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => i.FirstName.Contains(search) || i.LastName.Contains(search) || i.Email.Contains(search));

        ViewBag.DepartmentId = departmentId;
        ViewBag.Search = search;
        ViewBag.Term = term;
        ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(await query.OrderBy(i => i.LastName).ThenBy(i => i.FirstName).ToListAsync());
    }

    public async Task<IActionResult> Details(int id, int? termId)
    {
        var instructor = await _db.Instructors
            .Include(i => i.Department!).ThenInclude(d => d.Faculty)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (instructor == null) return NotFound();

        var term = termId.HasValue ? await _db.Terms.FindAsync(termId.Value) : await _registration.CurrentTermAsync();
        var sections = term == null ? new List<Section>() : await _registration.SectionsQuery()
            .Where(s => s.InstructorId == id && s.TermId == term.Id)
            .OrderBy(s => s.Subject!.Code).ThenBy(s => s.Number)
            .ToListAsync();

        ViewBag.Term = term;
        ViewBag.Terms = await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
        ViewBag.Sections = sections;
        ViewBag.Enrolled = await _registration.EnrolledCounts(sections.Select(s => s.Id));
        ViewBag.Timetable = ScheduleService.Entries(sections, showInstructor: false);
        ViewBag.HasLogin = instructor.UserId != null && await _users.FindByIdAsync(instructor.UserId) != null;
        return View(instructor);
    }

    public async Task<IActionResult> Create()
    {
        await LoadDepartments(null);
        return View(new Instructor());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Instructor instructor)
    {
        await Validate(instructor);
        if (!ModelState.IsValid)
        {
            await LoadDepartments(instructor.DepartmentId);
            return View(instructor);
        }

        instructor.UserId = null;
        _db.Instructors.Add(instructor);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("{0} added.", instructor.FullName);
        return RedirectToAction(nameof(Details), new { id = instructor.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var instructor = await _db.Instructors.FindAsync(id);
        if (instructor == null) return NotFound();
        await LoadDepartments(instructor.DepartmentId);
        return View(instructor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Instructor form)
    {
        if (id != form.Id) return BadRequest();
        var instructor = await _db.Instructors.FindAsync(id);
        if (instructor == null) return NotFound();

        await Validate(form);
        if (!ModelState.IsValid)
        {
            await LoadDepartments(form.DepartmentId);
            return View(form);
        }

        // Copy the editable fields; the linked login (UserId) is kept as it was.
        instructor.Title = form.Title;
        instructor.FirstName = form.FirstName;
        instructor.LastName = form.LastName;
        instructor.Email = form.Email;
        instructor.PhoneNumber = form.PhoneNumber;
        instructor.Office = form.Office;
        instructor.HireDate = form.HireDate;
        instructor.IsActive = form.IsActive;
        instructor.DepartmentId = form.DepartmentId;
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Instructor updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var instructor = await _db.Instructors.FindAsync(id);
        if (instructor == null) return NotFound();
        if (await _db.Sections.AnyAsync(s => s.InstructorId == id))
        {
            TempData["Error"] = T("{0} is assigned to sections. Reassign them first, or mark the instructor inactive.", instructor.FullName);
            return RedirectToAction(nameof(Details), new { id });
        }

        if (instructor.UserId != null && await _users.FindByIdAsync(instructor.UserId) is { } user)
            await _users.DeleteAsync(user);
        _db.Instructors.Remove(instructor);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("{0} deleted.", instructor.FullName);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Creates a login for the instructor with a one-time password shown to the admin.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLogin(int id)
    {
        var instructor = await _db.Instructors.FindAsync(id);
        if (instructor == null) return NotFound();

        var (userId, message) = await Accounts.CreateLoginAsync(_users, instructor.Email, SeedData.InstructorRole);
        if (userId != null)
        {
            instructor.UserId = userId;
            await _db.SaveChangesAsync();
            TempData["Success"] = message;
        }
        else TempData["Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task Validate(Instructor instructor)
    {
        if (!Instructor.Titles.Contains(instructor.Title))
            ModelState.AddModelError(nameof(Instructor.Title), T("Pick a title from the list."));
        if (!await _db.Departments.AnyAsync(d => d.Id == instructor.DepartmentId))
            ModelState.AddModelError(nameof(Instructor.DepartmentId), T("Select a department."));
        if (await _db.Instructors.AnyAsync(i => i.Email == instructor.Email && i.Id != instructor.Id))
            ModelState.AddModelError(nameof(Instructor.Email), T("Another instructor already uses this email."));
    }

    private async Task LoadDepartments(int? selected)
    {
        var depts = await _db.Departments.Include(d => d.Faculty).OrderBy(d => d.Faculty!.Name).ThenBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, Group = d.Faculty != null ? d.Faculty.Name : "No faculty" })
            .ToListAsync();
        ViewBag.Departments = new SelectList(depts, "Id", "Name", selected, "Group");
    }
}
