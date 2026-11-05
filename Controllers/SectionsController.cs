using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class SectionsController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;
    private readonly ScheduleService _schedule;

    public SectionsController(AppDbContext db, RegistrationService registration, ScheduleService schedule)
    {
        _db = db;
        _registration = registration;
        _schedule = schedule;
    }

    // GET: /Sections?termId=3&courseId=1&instructorId=2&view=timetable
    public async Task<IActionResult> Index(int? termId, int? courseId, int? instructorId, string? view)
    {
        var term = termId.HasValue ? await _db.Terms.FindAsync(termId.Value) : await _registration.CurrentTermAsync();
        var tid = term?.Id ?? 0;

        var query = _registration.SectionsQuery().Where(s => s.TermId == tid);
        if (courseId.HasValue) query = query.Where(s => s.Subject!.CourseId == courseId);
        if (instructorId.HasValue) query = query.Where(s => s.InstructorId == instructorId);
        var sections = await query.OrderBy(s => s.Subject!.Code).ThenBy(s => s.Number).ToListAsync();

        ViewBag.Term = term;
        ViewBag.CourseId = courseId;
        ViewBag.InstructorId = instructorId;
        ViewBag.View = view;
        ViewBag.Terms = await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
        ViewBag.Courses = await _db.Courses.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Instructors = await _db.Instructors.OrderBy(i => i.LastName).ToListAsync();
        ViewBag.Enrolled = await _registration.EnrolledCounts(sections.Select(s => s.Id));
        ViewBag.Timetable = ScheduleService.Entries(sections);
        return View(sections);
    }

    public async Task<IActionResult> Details(int id)
    {
        var section = await _registration.SectionsQuery()
            .Include(s => s.Subject!).ThenInclude(s => s.Course)
            .Include(s => s.Registrations).ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (section == null) return NotFound();

        // Students of the program who aren't in this section yet, for the "register a student" box.
        var inSection = section.Registrations.Select(r => r.StudentId).ToList();
        ViewBag.Candidates = await _db.Students
            .Where(s => s.CourseId == section.Subject!.CourseId && s.IsActive && !inSection.Contains(s.Id))
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new SelectListItem(s.FirstName + " " + s.LastName + " (" + s.AdmissionNumber + ")", s.Id.ToString()))
            .ToListAsync();
        ViewBag.Rooms = await _db.Rooms.OrderBy(r => r.Code).ToListAsync();
        ViewBag.Timetable = ScheduleService.Entries(new[] { section });
        return View(section);
    }

    public async Task<IActionResult> Create(int? termId, int? subjectId)
    {
        var term = termId.HasValue ? await _db.Terms.FindAsync(termId.Value) : await _registration.CurrentTermAsync();
        var section = new Section { TermId = term?.Id ?? 0, SubjectId = subjectId ?? 0 };
        await LoadLists(section);
        return View(section);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Section section)
    {
        await Validate(section);
        if (!ModelState.IsValid)
        {
            await LoadLists(section);
            return View(section);
        }
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Section created. Now add its weekly meetings.");
        return RedirectToAction(nameof(Details), new { id = section.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var section = await _db.Sections.FindAsync(id);
        if (section == null) return NotFound();
        await LoadLists(section);
        return View(section);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Section form)
    {
        if (id != form.Id) return BadRequest();
        var section = await _db.Sections.FindAsync(id);
        if (section == null) return NotFound();

        await Validate(form);
        if (form.InstructorId.HasValue && form.InstructorId != section.InstructorId)
            foreach (var p in await _schedule.CheckInstructorAsync(section, form.InstructorId.Value))
                ModelState.AddModelError(nameof(Section.InstructorId), p);

        var enrolled = await _db.Registrations.CountAsync(r => r.SectionId == id);
        if (form.Capacity < enrolled)
            ModelState.AddModelError(nameof(Section.Capacity), T("{0} students are already registered.", enrolled));
        if (form.SubjectId != section.SubjectId && enrolled > 0)
            ModelState.AddModelError(nameof(Section.SubjectId), T("The subject can't change once students have registered."));

        if (!ModelState.IsValid)
        {
            await LoadLists(form);
            return View(form);
        }

        section.SubjectId = form.SubjectId;
        section.TermId = form.TermId;
        section.Number = form.Number;
        section.Capacity = form.Capacity;
        section.InstructorId = form.InstructorId;
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Section updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var section = await _db.Sections.Include(s => s.Subject).FirstOrDefaultAsync(s => s.Id == id);
        if (section == null) return NotFound();
        if (await _db.Registrations.AnyAsync(r => r.SectionId == id))
        {
            TempData["Error"] = T("Students are registered in this section. Drop them first.");
            return RedirectToAction(nameof(Details), new { id });
        }
        var termId = section.TermId;
        _db.Sections.Remove(section);   // its meetings go with it
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Section {0} deleted.", $"{section.Subject?.Code}-{section.Number}");
        return RedirectToAction(nameof(Index), new { termId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMeeting(int id, DayOfWeek day, TimeOnly startTime, TimeOnly endTime, int? roomId, string type)
    {
        var section = await _db.Sections.FindAsync(id);
        if (section == null) return NotFound();

        var meeting = new SectionMeeting
        {
            SectionId = id, Day = day, StartTime = startTime, EndTime = endTime, RoomId = roomId,
            Type = SectionMeeting.Types.Contains(type) ? type : "Lecture",
        };
        var problems = await _schedule.CheckMeetingAsync(section, meeting);

        // Students already in the section mustn't end up with a clash either.
        if (!problems.Any())
        {
            var clashes = await _db.Registrations
                .Where(r => r.SectionId == id)
                .SelectMany(r => r.Student!.Registrations.Where(o => o.SectionId != id && o.Section!.TermId == section.TermId))
                .SelectMany(o => o.Section!.Meetings.Select(m => new { o.Student!.FirstName, o.Student.LastName, m.Day, m.StartTime, m.EndTime }))
                .Where(m => m.Day == day)
                .ToListAsync();
            var hit = clashes.FirstOrDefault(m => m.StartTime < endTime && startTime < m.EndTime);
            if (hit != null)
                problems.Add(T("{0} (registered here) has another class then.", $"{hit.FirstName} {hit.LastName}"));
        }

        if (problems.Any())
            TempData["Error"] = T("Meeting not added:") + " " + string.Join(" ", problems);
        else
        {
            _db.SectionMeetings.Add(meeting);
            await _db.SaveChangesAsync();
            TempData["Success"] = T("Added {0} {1}.", Day(day), meeting.TimeRange);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMeeting(int id)
    {
        var meeting = await _db.SectionMeetings.FindAsync(id);
        if (meeting == null) return NotFound();
        _db.SectionMeetings.Remove(meeting);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Meeting removed.");
        return RedirectToAction(nameof(Details), new { id = meeting.SectionId });
    }

    /// <summary>Admin registers a student here; every rule applies except "registration closed".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int id, int studentId)
    {
        var (ok, message) = await _registration.RegisterAsync(studentId, id, asAdmin: true);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Drop(int id, int registrationId)
    {
        var (ok, message) = await _registration.DropAsync(registrationId, asAdmin: true);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task Validate(Section section)
    {
        if (!await _db.Subjects.AnyAsync(s => s.Id == section.SubjectId))
            ModelState.AddModelError(nameof(Section.SubjectId), T("Select a subject."));
        if (!await _db.Terms.AnyAsync(t => t.Id == section.TermId))
            ModelState.AddModelError(nameof(Section.TermId), T("Select a term."));
        if (section.InstructorId.HasValue && !await _db.Instructors.AnyAsync(i => i.Id == section.InstructorId))
            ModelState.AddModelError(nameof(Section.InstructorId), T("Instructor not found."));
        if (await _db.Sections.AnyAsync(s => s.SubjectId == section.SubjectId && s.TermId == section.TermId
                                             && s.Number == section.Number && s.Id != section.Id))
            ModelState.AddModelError(nameof(Section.Number), T("Section {0} of this subject already exists in that term.", section.Number));
    }

    private async Task LoadLists(Section section)
    {
        var subjects = await _db.Subjects.Include(s => s.Course)
            .OrderBy(s => s.Course!.Name).ThenBy(s => s.Code)
            .Select(s => new { s.Id, Label = s.Code + " — " + s.Name, Group = s.Course!.Name })
            .ToListAsync();
        ViewBag.Subjects = new SelectList(subjects, "Id", "Label", section.SubjectId, "Group");
        // Term and instructor names are built in memory so they come out in the current language.
        ViewBag.Terms = new SelectList((await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync())
            .Select(t => new { t.Id, Label = t.Name }), "Id", "Label", section.TermId);
        ViewBag.Instructors = new SelectList((await _db.Instructors.Where(i => i.IsActive).Include(i => i.Department)
            .OrderBy(i => i.LastName).ToListAsync())
            .Select(i => new { i.Id, Label = i.FullName, Group = i.Department!.Name }), "Id", "Label", section.InstructorId, "Group");
    }
}
