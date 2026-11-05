using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;
using StudentManagement.ViewModels;

namespace StudentManagement.Controllers;

/// <summary>
/// Class registers are taken per section: the list is the students registered in it.
/// Admins can open any section; instructors only their own.
/// </summary>
[Authorize(Roles = "Admin,Instructor")]
public class AttendanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;
    private readonly UserManager<IdentityUser> _users;

    public AttendanceController(AppDbContext db, RegistrationService registration, UserManager<IdentityUser> users)
    {
        _db = db;
        _registration = registration;
        _users = users;
    }

    // GET: /Attendance?sectionId=4&date=2026-11-05  — the register for one section on one day
    public async Task<IActionResult> Index(int? sectionId, DateTime? date)
    {
        var instructorId = await CurrentInstructorId();
        if (instructorId == -1) return Forbid();

        var vm = new AttendanceSheetViewModel { SectionId = sectionId, Date = (date ?? DateTime.Today).Date };
        await LoadSections(instructorId);

        if (sectionId.HasValue)
        {
            var section = await _db.Sections.Include(s => s.Subject).Include(s => s.Term).FirstOrDefaultAsync(s => s.Id == sectionId);
            if (section == null) return NotFound();
            if (instructorId != null && section.InstructorId != instructorId) return Forbid();

            vm.SubjectId = section.SubjectId;
            vm.SubjectName = $"{section.Subject?.Code}-{section.Number} — {section.Subject?.Name}";
            vm.CourseName = section.Term?.Name;

            var existing = await _db.Attendances
                .Where(a => a.SubjectId == section.SubjectId && a.Date == vm.Date)
                .ToDictionaryAsync(a => a.StudentId, a => a.Status);

            vm.Rows = await _db.Registrations
                .Where(r => r.SectionId == section.Id)
                .Select(r => r.Student!)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .Select(s => new AttendanceRow
                {
                    StudentId = s.Id,
                    StudentName = s.FirstName + " " + s.LastName,
                    AdmissionNumber = s.AdmissionNumber,
                })
                .ToListAsync();

            foreach (var row in vm.Rows)
                row.Status = existing.GetValueOrDefault(row.StudentId);
        }

        return View(vm);
    }

    // POST: /Attendance/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AttendanceSheetViewModel vm)
    {
        var instructorId = await CurrentInstructorId();
        if (instructorId == -1) return Forbid();

        var date = vm.Date.Date;
        var section = vm.SectionId.HasValue
            ? await _db.Sections.Include(s => s.Subject).FirstOrDefaultAsync(s => s.Id == vm.SectionId.Value)
            : null;
        if (section == null) return BadRequest();
        if (instructorId != null && section.InstructorId != instructorId) return Forbid();

        if (date > DateTime.Today)
        {
            TempData["Error"] = T("Attendance can't be marked for a future date.");
            return RedirectToAction(nameof(Index), new { sectionId = section.Id, date = date.ToString("yyyy-MM-dd") });
        }

        // Only accept students registered in this section.
        var allowed = await _db.Registrations.Where(r => r.SectionId == section.Id).Select(r => r.StudentId).ToListAsync();

        var existing = await _db.Attendances
            .Where(a => a.SubjectId == section.SubjectId && a.Date == date && allowed.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        int marked = 0, cleared = 0;
        foreach (var row in vm.Rows.Where(r => allowed.Contains(r.StudentId)))
        {
            existing.TryGetValue(row.StudentId, out var record);

            if (string.IsNullOrEmpty(row.Status))
            {
                if (record != null) { _db.Attendances.Remove(record); cleared++; }
                continue;
            }

            if (!Attendance.Statuses.Contains(row.Status)) continue;

            if (record == null)
            {
                _db.Attendances.Add(new Attendance
                {
                    StudentId = row.StudentId,
                    SubjectId = section.SubjectId,
                    Date = date,
                    Status = row.Status,
                });
            }
            else
            {
                record.Status = row.Status;
            }
            marked++;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = T("Attendance saved for {0} on {1}: {2} marked.", $"{section.Subject?.Code}-{section.Number}", Date(date), marked)
                              + (cleared > 0 ? " " + T("{0} cleared.", cleared) : "");
        return RedirectToAction(nameof(Index), new { sectionId = section.Id, date = date.ToString("yyyy-MM-dd") });
    }

    // GET: /Attendance/Summary — totals per student, flagging anyone under the threshold
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Summary(int? courseId, int? subjectId, DateTime? from, DateTime? to, int threshold = 75)
    {
        threshold = Math.Clamp(threshold, 0, 100);

        var query = _db.Attendances.AsQueryable();
        if (courseId.HasValue)  query = query.Where(a => a.Student!.CourseId == courseId);
        if (subjectId.HasValue) query = query.Where(a => a.SubjectId == subjectId);
        if (from.HasValue)      query = query.Where(a => a.Date >= from.Value.Date);
        if (to.HasValue)        query = query.Where(a => a.Date <= to.Value.Date);

        var counts = await query
            .GroupBy(a => new { a.StudentId, a.Status })
            .Select(g => new { g.Key.StudentId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var studentIds = counts.Select(c => c.StudentId).Distinct().ToList();
        var students = await _db.Students
            .Include(s => s.Course)
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        var rows = counts
            .GroupBy(c => c.StudentId)
            .Select(g =>
            {
                int Count(string status) => g.Where(c => c.Status == status).Sum(c => c.Count);
                var s = students[g.Key];
                var row = new AttendanceSummaryRow
                {
                    StudentId = s.Id,
                    StudentName = s.FullName,
                    AdmissionNumber = s.AdmissionNumber,
                    CourseName = s.Course?.Name,
                    Present = Count("Present"),
                    Late = Count("Late"),
                    Absent = Count("Absent"),
                    Excused = Count("Excused"),
                };
                row.Rate = Attendance.Rate(row.Present, row.Late, row.Absent);
                return row;
            })
            .OrderBy(r => r.Rate ?? 101)          // lowest attendance first
            .ThenBy(r => r.StudentName)
            .ToList();

        ViewBag.CourseId  = courseId;
        ViewBag.SubjectId = subjectId;
        ViewBag.From      = from?.ToString("yyyy-MM-dd");
        ViewBag.To        = to?.ToString("yyyy-MM-dd");
        ViewBag.Threshold = threshold;
        ViewBag.Courses   = await _db.Courses.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Subjects  = await _db.Subjects.OrderBy(s => s.Code).ToListAsync();
        return View(rows);
    }

    /// <summary>Null for admins (any section); the instructor's id; or -1 for an instructor login with no staff record.</summary>
    private async Task<int?> CurrentInstructorId()
    {
        if (User.IsInRole("Admin")) return null;
        var userId = _users.GetUserId(User);
        return await _db.Instructors.Where(i => i.UserId == userId).Select(i => (int?)i.Id).FirstOrDefaultAsync() ?? -1;
    }

    /// <summary>Current-term sections for the dropdown: all of them for admins, only their own for instructors.</summary>
    private async Task LoadSections(int? instructorId)
    {
        var term = await _registration.CurrentTermAsync();
        var tid = term?.Id ?? 0;
        ViewBag.Term = term;
        ViewBag.Sections = await _db.Sections
            .Include(s => s.Subject!).ThenInclude(s => s.Course)
            .Where(s => s.TermId == tid && (instructorId == null || s.InstructorId == instructorId))
            .OrderBy(s => s.Subject!.Course!.Name).ThenBy(s => s.Subject!.Code).ThenBy(s => s.Number)
            .ToListAsync();
    }
}
