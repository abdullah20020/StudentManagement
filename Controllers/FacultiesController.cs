using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class FacultiesController : Controller
{
    private readonly AppDbContext _db;

    public FacultiesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var faculties = await _db.Faculties
            .Include(f => f.Departments).ThenInclude(d => d.Courses).ThenInclude(c => c.Students)
            .Include(f => f.Departments).ThenInclude(d => d.Instructors)
            .AsSplitQuery()
            .OrderBy(f => f.Name)
            .ToListAsync();
        ViewBag.Unassigned = await _db.Departments.CountAsync(d => d.FacultyId == null);
        return View(faculties);
    }

    public async Task<IActionResult> Details(int id)
    {
        var faculty = await _db.Faculties
            .Include(f => f.Departments).ThenInclude(d => d.Courses).ThenInclude(c => c.Students)
            .Include(f => f.Departments).ThenInclude(d => d.Instructors)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id);
        if (faculty == null) return NotFound();
        return View(faculty);
    }

    public IActionResult Create() => View(new Faculty());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Faculty faculty)
    {
        await ValidateUniqueCode(faculty);
        if (!ModelState.IsValid) return View(faculty);

        _db.Faculties.Add(faculty);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Faculty \"{0}\" created.", faculty.Name);
        return RedirectToAction(nameof(Details), new { id = faculty.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var faculty = await _db.Faculties.FindAsync(id);
        return faculty == null ? NotFound() : View(faculty);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Faculty faculty)
    {
        if (id != faculty.Id) return BadRequest();
        await ValidateUniqueCode(faculty);
        if (!ModelState.IsValid) return View(faculty);

        _db.Update(faculty);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Faculty updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var faculty = await _db.Faculties.FindAsync(id);
        if (faculty == null) return NotFound();
        if (await _db.Departments.AnyAsync(d => d.FacultyId == id))
        {
            TempData["Error"] = T("This faculty still has departments. Delete or move them first.");
            return RedirectToAction(nameof(Details), new { id });
        }
        _db.Faculties.Remove(faculty);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Faculty \"{0}\" deleted.", faculty.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDepartment(int id, Department department)
    {
        if (!await _db.Faculties.AnyAsync(f => f.Id == id)) return NotFound();
        department.Id = 0;
        department.FacultyId = id;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = T("Department not added:") + " " + string.Join(" ",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Departments.Add(department);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Department \"{0}\" added.", department.Name);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        var dept = await _db.Departments.FindAsync(id);
        if (dept == null) return NotFound();

        if (await _db.Courses.AnyAsync(c => c.DepartmentId == id) || await _db.Instructors.AnyAsync(i => i.DepartmentId == id))
            TempData["Error"] = T("\"{0}\" still has programs or instructors, so it can't be deleted.", dept.Name);
        else
        {
            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
            TempData["Success"] = T("Department \"{0}\" deleted.", dept.Name);
        }
        return RedirectToAction(nameof(Details), new { id = dept.FacultyId });
    }

    private async Task ValidateUniqueCode(Faculty faculty)
    {
        if (await _db.Faculties.AnyAsync(f => f.Code == faculty.Code && f.Id != faculty.Id))
            ModelState.AddModelError(nameof(Faculty.Code), T("Code \"{0}\" is already in use.", faculty.Code));
    }
}
