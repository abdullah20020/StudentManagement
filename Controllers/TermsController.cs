using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class TermsController : Controller
{
    private readonly AppDbContext _db;

    public TermsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var terms = await _db.Terms.Include(t => t.Sections).ThenInclude(s => s.Registrations)
            .OrderByDescending(t => t.StartDate).ToListAsync();
        ViewBag.GradeCounts = await _db.Grades.GroupBy(g => g.TermId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return View(terms);
    }

    public IActionResult Create() => View(new AcademicTerm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AcademicTerm term)
    {
        await Validate(term);
        if (!ModelState.IsValid) return View(term);

        if (term.IsCurrent) await ClearCurrent();
        _db.Terms.Add(term);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Term {0} created.", term.Name);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var term = await _db.Terms.FindAsync(id);
        return term == null ? NotFound() : View(term);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AcademicTerm term)
    {
        if (id != term.Id) return BadRequest();
        await Validate(term);
        if (!ModelState.IsValid) return View(term);

        if (term.IsCurrent) await ClearCurrent(except: id);
        _db.Update(term);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Term {0} updated.", term.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCurrent(int id)
    {
        var term = await _db.Terms.FindAsync(id);
        if (term == null) return NotFound();
        await ClearCurrent(except: id);
        term.IsCurrent = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = T("{0} is now the current term.", term.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRegistration(int id)
    {
        var term = await _db.Terms.FindAsync(id);
        if (term == null) return NotFound();
        term.RegistrationOpen = !term.RegistrationOpen;
        await _db.SaveChangesAsync();
        TempData["Success"] = (term.RegistrationOpen ? T("Registration for {0} is now open.", term.Name) : T("Registration for {0} is now closed.", term.Name));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var term = await _db.Terms.FindAsync(id);
        if (term == null) return NotFound();
        if (await _db.Sections.AnyAsync(s => s.TermId == id) || await _db.Grades.AnyAsync(g => g.TermId == id))
        {
            TempData["Error"] = T("{0} has sections or grades, so it can't be deleted.", term.Name);
            return RedirectToAction(nameof(Index));
        }
        _db.Terms.Remove(term);
        await _db.SaveChangesAsync();
        TempData["Success"] = T("Term {0} deleted.", term.Name);
        return RedirectToAction(nameof(Index));
    }

    private async Task Validate(AcademicTerm term)
    {
        if (!AcademicTerm.Seasons.Contains(term.Season))
            ModelState.AddModelError(nameof(AcademicTerm.Season), T("Pick Fall, Spring or Summer."));
        if (term.EndDate <= term.StartDate)
            ModelState.AddModelError(nameof(AcademicTerm.EndDate), T("The end date must be after the start date."));
        if (await _db.Terms.AnyAsync(t => t.Season == term.Season && t.Year == term.Year && t.Id != term.Id))
            ModelState.AddModelError(string.Empty, T("{0} already exists.", term.Name));
    }

    private async Task ClearCurrent(int except = 0)
    {
        foreach (var t in await _db.Terms.Where(t => t.IsCurrent && t.Id != except).ToListAsync())
            t.IsCurrent = false;
    }
}
