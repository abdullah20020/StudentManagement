using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class RoomsController : Controller
{
    private readonly AppDbContext _db;
    private readonly RegistrationService _registration;

    public RoomsController(AppDbContext db, RegistrationService registration)
    {
        _db = db;
        _registration = registration;
    }

    public async Task<IActionResult> Index()
    {
        var term = await _registration.CurrentTermAsync();
        var termId = term?.Id ?? 0;
        ViewBag.Term = term;
        ViewBag.Usage = await _db.SectionMeetings
            .Where(m => m.RoomId != null && m.Section!.TermId == termId)
            .GroupBy(m => m.RoomId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        return View(await _db.Rooms.OrderBy(r => r.Code).ToListAsync());
    }

    public async Task<IActionResult> Details(int id, int? termId)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();

        var term = termId.HasValue ? await _db.Terms.FindAsync(termId.Value) : await _registration.CurrentTermAsync();
        var tid = term?.Id ?? 0;
        var sections = await _registration.SectionsQuery()
            .Where(s => s.TermId == tid && s.Meetings.Any(m => m.RoomId == id))
            .ToListAsync();
        // Only the meetings held in this room.
        foreach (var s in sections) s.Meetings = s.Meetings.Where(m => m.RoomId == id).ToList();

        ViewBag.Term = term;
        ViewBag.Terms = await _db.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
        ViewBag.Timetable = ScheduleService.Entries(sections);
        return View(room);
    }

    public IActionResult Create() => View(new Room());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Room room)
    {
        await Validate(room);
        if (!ModelState.IsValid) return View(room);
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Room {0} added.", room.Code);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        return room == null ? NotFound() : View(room);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Room room)
    {
        if (id != room.Id) return BadRequest();
        await Validate(room);
        if (!ModelState.IsValid) return View(room);
        _db.Update(room);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Room {0} updated.", room.Code);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();
        if (await _db.SectionMeetings.AnyAsync(m => m.RoomId == id))
        {
            TempData["Error"] = T("Room {0} is used in the schedule, so it can't be deleted.", room.Code);
            return RedirectToAction(nameof(Index));
        }
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Room {0} deleted.", room.Code);
        return RedirectToAction(nameof(Index));
    }

    private async Task Validate(Room room)
    {
        if (!Room.Types.Contains(room.Type))
            ModelState.AddModelError(nameof(Room.Type), T("Pick a room type from the list."));
        if (await _db.Rooms.AnyAsync(r => r.Code == room.Code && r.Id != room.Id))
            ModelState.AddModelError(nameof(Room.Code), T("Room code \"{0}\" is already in use.", room.Code));
    }
}
