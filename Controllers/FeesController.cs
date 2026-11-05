using static StudentManagement.Localization.L;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.ViewModels;

namespace StudentManagement.Controllers;

[Authorize(Roles = "Admin")]
public class FeesController : Controller
{
    private readonly AppDbContext _db;

    public FeesController(AppDbContext db) => _db = db;

    // GET: /Fees â€” payment history
    public async Task<IActionResult> Index(int? studentId, string? term, int? year, string? method, string? search)
    {
        var query = _db.FeePayments.Include(f => f.Student).ThenInclude(s => s!.Course).AsQueryable();

        if (studentId.HasValue)          query = query.Where(f => f.StudentId == studentId);
        if (!string.IsNullOrEmpty(term))   query = query.Where(f => f.Term == term);
        if (year.HasValue)               query = query.Where(f => f.Year == year);
        if (!string.IsNullOrEmpty(method)) query = query.Where(f => f.PaymentMethod == method);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(f =>
                f.ReceiptNumber.Contains(search) ||
                (f.TransactionReference != null && f.TransactionReference.Contains(search)) ||
                f.Student!.FirstName.Contains(search) ||
                f.Student!.LastName.Contains(search) ||
                f.Student!.AdmissionNumber.Contains(search));

        var payments = await query.OrderByDescending(f => f.PaidOn).ThenByDescending(f => f.Id).ToListAsync();

        ViewBag.StudentId = studentId;
        ViewBag.Term      = term;
        ViewBag.Year      = year;
        ViewBag.Method    = method;
        ViewBag.Search    = search;
        ViewBag.Years     = await _db.FeePayments.Select(f => f.Year).Distinct().OrderByDescending(y => y).ToListAsync();
        if (studentId.HasValue)
            ViewBag.StudentName = (await _db.Students.FindAsync(studentId))?.FullName;

        return View(payments);
    }

    // GET: /Fees/Outstanding â€” students whose latest payment still leaves a balance
    public async Task<IActionResult> Outstanding()
    {
        var payments = await _db.FeePayments
            .Include(f => f.Student).ThenInclude(s => s!.Course)
            .ToListAsync();

        var latest = payments
            .GroupBy(f => f.StudentId)
            .Select(g => g.OrderByDescending(f => f.PaidOn).ThenByDescending(f => f.Id).First())
            .Where(f => f.Balance > 0)
            .OrderByDescending(f => f.Balance)
            .ToList();

        return View(latest);
    }

    // GET: /Fees/Receipt/5
    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await _db.FeePayments
            .Include(f => f.Student).ThenInclude(s => s!.Course)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (payment == null) return NotFound();
        return View(payment);
    }

    // GET: /Fees/Create?studentId=5
    public async Task<IActionResult> Create(int? studentId, string? returnUrl)
    {
        var vm = new FeePaymentViewModel { StudentId = studentId, ReturnUrl = returnUrl };
        await LoadForm(vm.StudentId);
        return View(vm);
    }

    // POST: /Fees/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FeePaymentViewModel vm)
    {
        if (!FeePaymentViewModel.Terms.Contains(vm.Term))
            ModelState.AddModelError(nameof(vm.Term), T("Pick a term from the list."));
        if (!FeePaymentViewModel.Methods.Contains(vm.PaymentMethod))
            ModelState.AddModelError(nameof(vm.PaymentMethod), T("Pick a payment method from the list."));
        if (vm.PaidOn.Date > DateTime.Today)
            ModelState.AddModelError(nameof(vm.PaidOn), T("Payment date can't be in the future."));

        var student = vm.StudentId.HasValue ? await _db.Students.FindAsync(vm.StudentId.Value) : null;
        if (vm.StudentId.HasValue && student == null)
            ModelState.AddModelError(nameof(vm.StudentId), T("Student not found."));

        if (!ModelState.IsValid)
        {
            await LoadForm(vm.StudentId);
            return View(vm);
        }

        var payment = new FeePayment
        {
            StudentId            = student!.Id,
            ReceiptNumber        = await NextReceiptNumber(vm.Year),
            Amount               = vm.Amount!.Value,
            Balance              = vm.Balance!.Value,
            Term                 = vm.Term,
            Year                 = vm.Year,
            PaymentMethod        = vm.PaymentMethod,
            TransactionReference = string.IsNullOrWhiteSpace(vm.TransactionReference) ? null : vm.TransactionReference.Trim(),
            PaidOn               = vm.PaidOn.Date,
        };
        _db.FeePayments.Add(payment);
        await _db.SaveChangesAsync();

        TempData["Success"] = T("Payment of {0} recorded for {1}. Receipt {2}.", payment.Amount.ToString("N2"), student.FullName, payment.ReceiptNumber);
        return RedirectToAction(nameof(Receipt), new { id = payment.Id });
    }

    // POST: /Fees/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var payment = await _db.FeePayments.FindAsync(id);
        if (payment != null)
        {
            _db.FeePayments.Remove(payment);
            await _db.SaveChangesAsync();
            TempData["Success"] = T("Payment {0} deleted.", payment.ReceiptNumber);
        }
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }

    /// <summary>RCP/{year}/{n}, continuing from the highest number already used that year.</summary>
    private async Task<string> NextReceiptNumber(int year)
    {
        var prefix = $"RCP/{year}/";
        var used = await _db.FeePayments
            .Where(f => f.ReceiptNumber.StartsWith(prefix))
            .Select(f => f.ReceiptNumber)
            .ToListAsync();
        var max = used
            .Select(r => int.TryParse(r[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{max + 1:D4}";
    }

    private async Task LoadForm(int? studentId)
    {
        var students = await _db.Students
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, Label = s.FirstName + " " + s.LastName + " (" + s.AdmissionNumber + ")" })
            .ToListAsync();
        ViewBag.Students = new SelectList(students, "Id", "Label", studentId);

        if (studentId.HasValue)
        {
            ViewBag.LastPayment = await _db.FeePayments
                .Where(f => f.StudentId == studentId)
                .OrderByDescending(f => f.PaidOn).ThenByDescending(f => f.Id)
                .FirstOrDefaultAsync();
        }
    }
}
