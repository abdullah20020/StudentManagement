using OfficeOpenXml;
using OfficeOpenXml.Style;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using StudentManagement.Data;
using StudentManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Drawing;

namespace StudentManagement.Services
{
    public class ReportService
    {
        private readonly AppDbContext _db;

        public ReportService(AppDbContext db)
        {
            _db = db;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ── Excel: Student List ───────────────────────────────────────────────
        public async Task<byte[]> ExportStudentsExcelAsync(List<Student> students)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Students");

            // Title
            ws.Cells["A1:K1"].Merge = true;
            ws.Cells["A1"].Value = "Student Management System — Student List";
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.Font.Size = 14;
            ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells["A2"].Value = $"Generated: {DateTime.Now:dd MMM yyyy HH:mm}";
            ws.Cells["A2:K2"].Merge = true;
            ws.Cells["A2"].Style.Font.Italic = true;
            ws.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Headers
            var headers = new[] { "#", "Admission No", "First Name", "Last Name", "Email", "Phone", "Gender", "Course", "Department", "Enrolled On", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[4, i + 1].Value = headers[i];
                ws.Cells[4, i + 1].Style.Font.Bold = true;
                ws.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                ws.Cells[4, i + 1].Style.Font.Color.SetColor(Color.White);
                ws.Cells[4, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data rows
            for (int r = 0; r < students.Count; r++)
            {
                var s   = students[r];
                var row = r + 5;
                ws.Cells[row, 1].Value  = r + 1;
                ws.Cells[row, 2].Value  = s.AdmissionNumber;
                ws.Cells[row, 3].Value  = s.FirstName;
                ws.Cells[row, 4].Value  = s.LastName;
                ws.Cells[row, 5].Value  = s.Email;
                ws.Cells[row, 6].Value  = s.PhoneNumber;
                ws.Cells[row, 7].Value  = s.Gender;
                ws.Cells[row, 8].Value  = s.Course?.Name;
                ws.Cells[row, 9].Value  = s.Course?.Department?.Name;
                ws.Cells[row, 10].Value = s.EnrollmentDate.ToString("dd/MM/yyyy");
                ws.Cells[row, 11].Value = s.IsActive ? "Active" : "Inactive";

                if (r % 2 == 0)
                {
                    var fillRange = ws.Cells[row, 1, row, 11];
                    fillRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    fillRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(235, 241, 250));
                }
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            return await package.GetAsByteArrayAsync();
        }

        // ── Excel: Grade Sheet ─────────────────────────────────────────────────
        public async Task<byte[]> ExportGradeSheetExcelAsync(int courseId, string semester, int year)
        {
            var grades = await _db.Grades
                .Include(g => g.Student)
                .Include(g => g.Subject)
                .Where(g => g.Subject!.CourseId == courseId && g.Semester == semester && g.Year == year)
                .OrderBy(g => g.Student!.LastName)
                .ToListAsync();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Grade Sheet");

            var headers = new[] { "#", "Admission No", "Student Name", "Subject", "Score", "Letter Grade", "Points", "Semester", "Year" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                ws.Cells[1, i + 1].Style.Font.Color.SetColor(Color.White);
            }

            for (int r = 0; r < grades.Count; r++)
            {
                var g   = grades[r];
                var row = r + 2;
                ws.Cells[row, 1].Value = r + 1;
                ws.Cells[row, 2].Value = g.Student?.AdmissionNumber;
                ws.Cells[row, 3].Value = g.Student?.FullName;
                ws.Cells[row, 4].Value = g.Subject?.Name;
                ws.Cells[row, 5].Value = g.Score;
                ws.Cells[row, 6].Value = g.LetterGrade;
                ws.Cells[row, 7].Value = g.Points;
                ws.Cells[row, 8].Value = g.Semester;
                ws.Cells[row, 9].Value = g.Year;
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            return await package.GetAsByteArrayAsync();
        }

        // ── PDF: Student List ──────────────────────────────────────────────────
        public byte[] ExportStudentsPdf(List<Student> students)
        {
            using var ms  = new MemoryStream();
            using var pdf = new PdfDocument(new PdfWriter(ms));
            var doc = new Document(pdf, iText.Kernel.Geom.PageSize.A4.Rotate());

            var bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var navy   = new DeviceRgb(31, 73, 125);
            var white  = ColorConstants.WHITE;
            var light  = new DeviceRgb(235, 241, 250);

            // Title
            doc.Add(new Paragraph("Student Management System")
                .SetFont(bold).SetFontSize(16).SetFontColor(navy)
                .SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph($"Student List — Generated {DateTime.Now:dd MMM yyyy HH:mm}")
                .SetFont(normal).SetFontSize(9).SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(12));

            // Table
            float[] widths = { 1.5f, 4f, 4f, 4f, 5f, 3.5f, 3f, 6f, 2.5f };
            var table = new Table(UnitValue.CreatePercentArray(widths)).UseAllAvailableWidth();

            var headerCells = new[] { "#", "Admission No", "First Name", "Last Name", "Email", "Phone", "Gender", "Course", "Status" };
            foreach (var h in headerCells)
            {
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(h).SetFont(bold).SetFontSize(8).SetFontColor(white))
                    .SetBackgroundColor(navy).SetPadding(5));
            }

            for (int i = 0; i < students.Count; i++)
            {
                var s  = students[i];
                var bg = i % 2 == 0 ? light : ColorConstants.WHITE;

                void AddCell(string val) =>
                    table.AddCell(new Cell()
                        .Add(new Paragraph(val ?? "").SetFont(normal).SetFontSize(7.5f))
                        .SetBackgroundColor(bg).SetPadding(4));

                AddCell((i + 1).ToString());
                AddCell(s.AdmissionNumber);
                AddCell(s.FirstName);
                AddCell(s.LastName);
                AddCell(s.Email);
                AddCell(s.PhoneNumber ?? "—");
                AddCell(s.Gender);
                AddCell(s.Course?.Name ?? "—");
                AddCell(s.IsActive ? "Active" : "Inactive");
            }

            doc.Add(table);
            doc.Add(new Paragraph($"Total: {students.Count} student(s)")
                .SetFont(bold).SetFontSize(9).SetMarginTop(8));

            doc.Close();
            return ms.ToArray();
        }

        // ── PDF: Fee Statement ────────────────────────────────────────────────
        public byte[] ExportFeeStatementPdf(Student student, List<FeePayment> payments)
        {
            using var ms  = new MemoryStream();
            using var pdf = new PdfDocument(new PdfWriter(ms));
            var doc = new Document(pdf, iText.Kernel.Geom.PageSize.A4);

            var bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var navy   = new DeviceRgb(31, 73, 125);
            var light  = new DeviceRgb(235, 241, 250);

            // Header
            doc.Add(new Paragraph("FEE STATEMENT")
                .SetFont(bold).SetFontSize(18).SetFontColor(navy).SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph($"Student: {student.FullName}   |   Admission: {student.AdmissionNumber}")
                .SetFont(normal).SetFontSize(10).SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(16));

            // Student info block
            var infoTable = new Table(2).UseAllAvailableWidth().SetMarginBottom(16);
            void AddInfo(string label, string value)
            {
                infoTable.AddCell(new Cell().Add(new Paragraph(label).SetFont(bold).SetFontSize(9)).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                infoTable.AddCell(new Cell().Add(new Paragraph(value ?? "—").SetFont(normal).SetFontSize(9)).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            }
            AddInfo("Course:",  student.Course?.Name ?? "—");
            AddInfo("Email:",   student.Email);
            AddInfo("Phone:",   student.PhoneNumber ?? "—");
            AddInfo("Generated:", DateTime.Now.ToString("dd MMM yyyy HH:mm"));
            doc.Add(infoTable);

            // Payments table
            float[] widths = { 2f, 3f, 4f, 4f, 3f, 4f, 3f };
            var table = new Table(UnitValue.CreatePercentArray(widths)).UseAllAvailableWidth();
            var headers = new[] { "#", "Receipt", "Amount (KES)", "Balance (KES)", "Term", "Method", "Date" };

            foreach (var h in headers)
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(h).SetFont(bold).SetFontSize(8.5f).SetFontColor(ColorConstants.WHITE))
                    .SetBackgroundColor(navy).SetPadding(5));

            for (int i = 0; i < payments.Count; i++)
            {
                var p  = payments[i];
                var bg = i % 2 == 0 ? light : ColorConstants.WHITE;

                void AddCell(string val) =>
                    table.AddCell(new Cell()
                        .Add(new Paragraph(val).SetFont(normal).SetFontSize(8.5f))
                        .SetBackgroundColor(bg).SetPadding(4));

                AddCell((i + 1).ToString());
                AddCell(p.ReceiptNumber);
                AddCell(p.Amount.ToString("N2"));
                AddCell(p.Balance.ToString("N2"));
                AddCell(p.Term);
                AddCell(p.PaymentMethod);
                AddCell(p.PaidOn.ToString("dd/MM/yyyy"));
            }

            doc.Add(table);

            // Summary
            var totalPaid    = payments.Sum(p => p.Amount);
            var totalBalance = payments.Sum(p => p.Balance);
            doc.Add(new Paragraph($"\nTotal Paid: KES {totalPaid:N2}   |   Outstanding Balance: KES {totalBalance:N2}")
                .SetFont(bold).SetFontSize(10).SetFontColor(navy).SetTextAlignment(TextAlignment.RIGHT));

            doc.Close();
            return ms.ToArray();
        }
    }
}