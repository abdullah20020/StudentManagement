using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

public class GradeSheetRow
{
    public int StudentId { get; set; }
    public decimal? Score { get; set; }
}

/// <summary>The signed-in instructor's sections, rosters and grade entry. Only their own sections.</summary>
[Authorize(Roles = "Instructor")]
public class InstructorPortalController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;
    private readonly UserManager<IdentityUser> _users;

    public InstructorPortalController(AppDbContext db, RegistrationService registration, UserManager<IdentityUser> users)
    {
        _db = db;
        _registration = registration;
        _users = users;
    }

    public async Task<IActionResult> Index(int? termId)
    {
        var me = await CurrentInstructor();
        if (me == null) return View("NoProfile");

        var term = termId.HasValue ? await _db.Terms.FindAsync(termId.Value) : await _registration.CurrentTermAsync();
        var tid = term?.Id ?? 0;
        var sections = await _registration.SectionsQuery()
            .Where(s => s.InstructorId == me.Id && s.TermId == tid)
            .OrderBy(s => s.Subject!.Code).ThenBy(s => s.Number)
            .ToListAsync();

        ViewBag.Instructor = me;
        ViewBag.Term = term;
        ViewBag.Terms = await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
        ViewBag.Enrolled = await _registration.EnrolledCounts(sections.Select(s => s.Id));
        ViewBag.Timetable = ScheduleService.Entries(sections, showInstructor: false);
        return View(sections);
    }

    /// <summary>Class roster with a grade sheet for the section's term.</summary>
    public async Task<IActionResult> Section(int id)
    {
        var (section, error) = await MySection(id);
        if (section == null) return error!;

        var roster = await _db.Registrations
            .Include(r => r.Student)
            .Where(r => r.SectionId == id)
            .Select(r => r.Student!)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();
        var ids = roster.Select(s => s.Id).ToList();

        ViewBag.Grades = await _db.Grades
            .Where(g => g.SubjectId == section.SubjectId && g.TermId == section.TermId && ids.Contains(g.StudentId))
            .ToDictionaryAsync(g => g.StudentId);
        var att = await _db.Attendances
            .Where(a => a.SubjectId == section.SubjectId && ids.Contains(a.StudentId)
                        && a.Date >= section.Term!.StartDate && a.Date <= section.Term.EndDate)
            .ToListAsync();
        ViewBag.Attendance = att.GroupBy(a => a.StudentId).ToDictionary(g => g.Key,
            g => Attendance.Rate(g.Count(a => a.Status == "Present"), g.Count(a => a.Status == "Late"), g.Count(a => a.Status == "Absent")));
        ViewBag.Roster = roster;
        return View(section);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGrades(int id, List<GradeSheetRow> rows)
    {
        var (section, error) = await MySection(id);
        if (section == null) return error!;

        var roster = await _db.Registrations.Where(r => r.SectionId == id).Select(r => r.StudentId).ToListAsync();
        var existing = await _db.Grades
            .Where(g => g.SubjectId == section.SubjectId && g.TermId == section.TermId && roster.Contains(g.StudentId))
            .ToDictionaryAsync(g => g.StudentId);

        int saved = 0, removed = 0;
        var invalid = new List<string>();
        foreach (var row in rows.Where(r => roster.Contains(r.StudentId)))
        {
            existing.TryGetValue(row.StudentId, out var grade);
            if (row.Score == null)
            {
                if (grade != null) { _db.Grades.Remove(grade); removed++; }
                continue;
            }
            if (row.Score < 0 || row.Score > 100) { invalid.Add(row.Score.Value.ToString("0.#")); continue; }

            var score = row.Score.Value;
            if (grade == null)
            {
                grade = new Grade { StudentId = row.StudentId, SubjectId = section.SubjectId, TermId = section.TermId };
                _db.Grades.Add(grade);
            }
            grade.Score = score;
            grade.LetterGrade = Grade.ComputeLetter(score);
            grade.Points = Grade.ComputePoints(score);
            saved++;
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = T("Grades saved: {0}.", saved) + (removed > 0 ? " " + T("{0} cleared.", removed) : "");
        if (invalid.Any())
            TempData["Error"] = T("Skipped {0} score(s) outside 0–100: {1}.", invalid.Count, string.Join(", ", invalid));
        return RedirectToAction(nameof(Section), new { id });
    }

    private async Task<Instructor?> CurrentInstructor()
    {
        var userId = _users.GetUserId(User);
        return await _db.Instructors.Include(i => i.Department).FirstOrDefaultAsync(i => i.UserId == userId);
    }

    /// <summary>Loads the section if it belongs to the signed-in instructor.</summary>
    private async Task<(Section?, IActionResult?)> MySection(int id)
    {
        var me = await CurrentInstructor();
        if (me == null) return (null, Forbid());
        var section = await _registration.SectionsQuery().FirstOrDefaultAsync(s => s.Id == id);
        if (section == null) return (null, NotFound());
        if (section.InstructorId != me.Id) return (null, Forbid());
        return (section, null);
    }
}
