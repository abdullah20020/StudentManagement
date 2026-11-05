using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;
using StudentManagement.ViewModels;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class GradesController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;

    public GradesController(AppDbContext db, RegistrationService registration)
    {
        _db = db;
        _registration = registration;
    }

    // GET: /Grades
    public async Task<IActionResult> Index(int? courseId, int? subjectId, int? studentId, int? termId, string? letter)
    {
        var query = _db.Grades
            .Include(g => g.Student)
            .Include(g => g.Term)
            .Include(g => g.Subject).ThenInclude(s => s!.Course)
            .AsQueryable();

        if (courseId.HasValue)  query = query.Where(g => g.Subject!.CourseId == courseId);
        if (subjectId.HasValue) query = query.Where(g => g.SubjectId == subjectId);
        if (studentId.HasValue) query = query.Where(g => g.StudentId == studentId);
        if (termId.HasValue)    query = query.Where(g => g.TermId == termId);
        if (!string.IsNullOrEmpty(letter)) query = query.Where(g => g.LetterGrade == letter);

        var grades = await query
            .OrderBy(g => g.Student!.LastName).ThenBy(g => g.Student!.FirstName)
            .ThenBy(g => g.Term!.StartDate).ThenBy(g => g.Subject!.Code)
            .ToListAsync();

        ViewBag.CourseId  = courseId;
        ViewBag.SubjectId = subjectId;
        ViewBag.StudentId = studentId;
        ViewBag.TermId    = termId;
        ViewBag.Letter    = letter;
        ViewBag.Courses   = await _db.Courses.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Subjects  = await _db.Subjects.OrderBy(s => s.Code).ToListAsync();
        ViewBag.Terms     = await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
        if (studentId.HasValue)
            ViewBag.StudentName = (await _db.Students.FindAsync(studentId))?.FullName;

        return View(grades);
    }

    // GET: /Grades/Create?studentId=5
    public async Task<IActionResult> Create(int? studentId, int? subjectId, int? termId, string? returnUrl)
    {
        var vm = new GradeViewModel
        {
            StudentId = studentId,
            SubjectId = subjectId,
            TermId = termId ?? (await _registration.CurrentTermAsync())?.Id,
            ReturnUrl = returnUrl,
        };
        await LoadLists(vm.StudentId);
        return View(vm);
    }

    // POST: /Grades/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GradeViewModel vm)
    {
        var (student, subject, term) = await ValidateGrade(vm);
        if (!ModelState.IsValid)
        {
            await LoadLists(vm.StudentId);
            return View(vm);
        }

        var score = vm.Score!.Value;
        _db.Grades.Add(new Grade
        {
            StudentId   = student!.Id,
            SubjectId   = subject!.Id,
            TermId      = term!.Id,
            Score       = score,
            LetterGrade = Grade.ComputeLetter(score),
            Points      = Grade.ComputePoints(score),
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = T("Grade {0} recorded for {1} in {2} ({3}).", Grade.ComputeLetter(score), student.FullName, subject.Name, term.Name);
        return BackTo(vm.ReturnUrl);
    }

    // GET: /Grades/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl)
    {
        var grade = await _db.Grades.FindAsync(id);
        if (grade == null) return NotFound();

        var vm = new GradeViewModel
        {
            Id        = grade.Id,
            StudentId = grade.StudentId,
            SubjectId = grade.SubjectId,
            Score     = grade.Score,
            TermId    = grade.TermId,
            ReturnUrl = returnUrl,
        };
        await LoadLists(vm.StudentId);
        return View(vm);
    }

    // POST: /Grades/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, GradeViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        var grade = await _db.Grades.FindAsync(id);
        if (grade == null) return NotFound();

        var (student, subject, term) = await ValidateGrade(vm);
        if (!ModelState.IsValid)
        {
            await LoadLists(vm.StudentId);
            return View(vm);
        }

        var score = vm.Score!.Value;
        grade.StudentId   = student!.Id;
        grade.SubjectId   = subject!.Id;
        grade.TermId      = term!.Id;
        grade.Score       = score;
        grade.LetterGrade = Grade.ComputeLetter(score);
        grade.Points      = Grade.ComputePoints(score);
        await _db.SaveChangesAsync();

        TempData["Success"] = T("Grade updated.");
        return BackTo(vm.ReturnUrl);
    }

    // POST: /Grades/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var grade = await _db.Grades.FindAsync(id);
        if (grade != null)
        {
            _db.Grades.Remove(grade);
            await _db.SaveChangesAsync();
            TempData["Success"] = T("Grade deleted.");
        }
        return BackTo(returnUrl);
    }

    /// <summary>Checks the student, subject and term exist, belong together, and aren't already graded for that term.</summary>
    private async Task<(Student?, Subject?, AcademicTerm?)> ValidateGrade(GradeViewModel vm)
    {
        var student = vm.StudentId.HasValue ? await _db.Students.FindAsync(vm.StudentId.Value) : null;
        var subject = vm.SubjectId.HasValue ? await _db.Subjects.FindAsync(vm.SubjectId.Value) : null;
        var term    = vm.TermId.HasValue ? await _db.Terms.FindAsync(vm.TermId.Value) : null;

        if (vm.StudentId.HasValue && student == null)
            ModelState.AddModelError(nameof(vm.StudentId), T("Student not found."));
        if (vm.SubjectId.HasValue && subject == null)
            ModelState.AddModelError(nameof(vm.SubjectId), T("Subject not found."));
        if (vm.TermId.HasValue && term == null)
            ModelState.AddModelError(nameof(vm.TermId), T("Term not found."));

        if (student != null && subject != null && term != null)
        {
            if (subject.CourseId != student.CourseId)
                ModelState.AddModelError(nameof(vm.SubjectId), T("This subject isn't part of the student's program."));

            var duplicate = await _db.Grades.AnyAsync(g =>
                g.Id != vm.Id && g.StudentId == student.Id && g.SubjectId == subject.Id && g.TermId == term.Id);
            if (duplicate)
                ModelState.AddModelError(string.Empty,
                    T("{0} already has a grade for {1} in {2}. Edit that one instead.", student.FullName, subject.Name, term.Name));
        }

        return (student, subject, term);
    }

    private async Task LoadLists(int? studentId)
    {
        var students = await _db.Students
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.CourseId, Label = s.FirstName + " " + s.LastName + " (" + s.AdmissionNumber + ")" })
            .ToListAsync();
        ViewBag.Students = new SelectList(students, "Id", "Label", studentId);

        // Only offer subjects from the selected student's program, when there is one.
        var courseId = students.FirstOrDefault(s => s.Id == studentId)?.CourseId;
        var subjects = await _db.Subjects
            .Where(s => courseId == null || s.CourseId == courseId)
            .OrderBy(s => s.Course!.Name).ThenBy(s => s.YearOfStudy).ThenBy(s => s.Code)
            .Select(s => new { s.Id, Label = s.Code + " — " + s.Name, Group = s.Course!.Name })
            .ToListAsync();
        ViewBag.Subjects = new SelectList(subjects, "Id", "Label", null, "Group");

        ViewBag.Terms = new SelectList((await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync())
            .Select(t => new { t.Id, Label = t.Name }), "Id", "Label");
    }

    private IActionResult BackTo(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
