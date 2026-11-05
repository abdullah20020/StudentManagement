using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class CoursesController : Controller
{
    private readonly AppDbContext _db;

    public CoursesController(AppDbContext db) => _db = db;

    // GET: /Courses
    public async Task<IActionResult> Index(int? departmentId)
    {
        var query = _db.Courses
            .Include(c => c.Department)
            .Include(c => c.Students)
            .Include(c => c.Subjects)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(c => c.DepartmentId == departmentId);

        ViewBag.DepartmentId = departmentId;
        ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    // GET: /Courses/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var course = await _db.Courses
            .Include(c => c.Department)
            .Include(c => c.Subjects).ThenInclude(s => s.Prerequisites).ThenInclude(p => p.Prerequisite)
            .Include(c => c.Subjects).ThenInclude(s => s.Sections)
            .Include(c => c.Students)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course == null) return NotFound();
        return View(course);
    }

    // GET: /Courses/Create
    public IActionResult Create()
    {
        LoadDepartments();
        return View(new Course());
    }

    // POST: /Courses/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Course course)
    {
        await ValidateUniqueCode(course);
        if (!ModelState.IsValid)
        {
            LoadDepartments(course.DepartmentId);
            return View(course);
        }

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Program \"{0}\" created.", course.Name);
        return RedirectToAction(nameof(Details), new { id = course.Id });
    }

    // GET: /Courses/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();
        LoadDepartments(course.DepartmentId);
        return View(course);
    }

    // POST: /Courses/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Course course)
    {
        if (id != course.Id) return BadRequest();

        await ValidateUniqueCode(course);
        if (!ModelState.IsValid)
        {
            LoadDepartments(course.DepartmentId);
            return View(course);
        }

        _db.Update(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Program updated.");
        return RedirectToAction(nameof(Details), new { id = course.Id });
    }

    // POST: /Courses/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();

        if (await _db.Students.AnyAsync(s => s.CourseId == id))
        {
            TempData["Error"] = T("This program still has students. Move or delete them first.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Courses.Remove(course);   // its subjects are removed with it (cascade)
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Program \"{0}\" deleted.", course.Name);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Courses/AddSubject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubject(int id, Subject subject)
    {
        if (!await _db.Courses.AnyAsync(c => c.Id == id)) return NotFound();

        subject.Id = 0;
        subject.CourseId = id;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = T("Subject not added:") + " " + string.Join(" ",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Subject \"{0}\" added.", subject.Name);
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Courses/DeleteSubject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubject(int id)
    {
        var subject = await _db.Subjects.FindAsync(id);
        if (subject == null) return NotFound();

        if (await _db.Grades.AnyAsync(g => g.SubjectId == id) || await _db.Sections.AnyAsync(s => s.SubjectId == id))
        {
            TempData["Error"] = T("\"{0}\" has grades or sections, so it can't be deleted.", subject.Name);
            return RedirectToAction(nameof(Details), new { id = subject.CourseId });
        }

        // Other subjects may list this one as a prerequisite; remove those links first.
        _db.SubjectPrerequisites.RemoveRange(_db.SubjectPrerequisites.Where(p => p.PrerequisiteId == id));
        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Subject \"{0}\" removed.", subject.Name);
        return RedirectToAction(nameof(Details), new { id = subject.CourseId });
    }

    // POST: /Courses/AddPrerequisite/5  (5 = the subject that gets the prerequisite)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPrerequisite(int id, int prerequisiteId)
    {
        var subject = await _db.Subjects.FindAsync(id);
        var prereq = await _db.Subjects.FindAsync(prerequisiteId);
        if (subject == null || prereq == null) return NotFound();

        if (id == prerequisiteId)
            TempData["Error"] = T("A subject can't be its own prerequisite.");
        else if (await _db.SubjectPrerequisites.AnyAsync(p => p.SubjectId == id && p.PrerequisiteId == prerequisiteId))
            TempData["Error"] = T("{0} is already a prerequisite of {1}.", prereq.Code, subject.Code);
        else if (await DependsOn(prerequisiteId, id))
            TempData["Error"] = T("{0} already requires {1} (directly or indirectly), so this would make a loop.", prereq.Code, subject.Code);
        else
        {
            _db.SubjectPrerequisites.Add(new SubjectPrerequisite { SubjectId = id, PrerequisiteId = prerequisiteId });
            await _db.SaveChangesAsync();
            TempData["Success"] = T("{0} now requires {1}.", subject.Code, prereq.Code);
        }
        return RedirectToAction(nameof(Details), new { id = subject.CourseId });
    }

    // POST: /Courses/RemovePrerequisite/5?prerequisiteId=2
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePrerequisite(int id, int prerequisiteId)
    {
        var link = await _db.SubjectPrerequisites.Include(p => p.Subject)
            .FirstOrDefaultAsync(p => p.SubjectId == id && p.PrerequisiteId == prerequisiteId);
        if (link == null) return NotFound();
        _db.SubjectPrerequisites.Remove(link);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Prerequisite removed.");
        return RedirectToAction(nameof(Details), new { id = link.Subject!.CourseId });
    }

    /// <summary>True if <paramref name="subjectId"/> requires <paramref name="target"/>, following the prerequisite chain.</summary>
    private async Task<bool> DependsOn(int subjectId, int target)
    {
        var links = await _db.SubjectPrerequisites.ToListAsync();
        var seen = new HashSet<int>();
        var stack = new Stack<int>(new[] { subjectId });
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == target) return true;
            if (!seen.Add(current)) continue;
            foreach (var l in links.Where(l => l.SubjectId == current)) stack.Push(l.PrerequisiteId);
        }
        return false;
    }

    private void LoadDepartments(int? selected = null) =>
        ViewBag.Departments = new SelectList(_db.Departments.OrderBy(d => d.Name), "Id", "Name", selected);

    private async Task ValidateUniqueCode(Course course)
    {
        if (await _db.Courses.AnyAsync(c => c.Code == course.Code && c.Id != course.Id))
            ModelState.AddModelError(nameof(Course.Code), T("Program code \"{0}\" is already in use.", course.Code));
    }
}
