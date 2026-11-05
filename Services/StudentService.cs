using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.ViewModels;

namespace StudentManagement.Services
{
    public class StudentService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public StudentService(AppDbContext db, IWebHostEnvironment env)
        {
            _db  = db;
            _env = env;
        }

        public async Task<List<Student>> GetAllAsync(string? search = null, int? courseId = null, bool? active = null)
        {
            var q = _db.Students.Include(s => s.Course).ThenInclude(c => c!.Department).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(s =>
                    s.FirstName.Contains(search) ||
                    s.LastName.Contains(search)  ||
                    s.AdmissionNumber.Contains(search) ||
                    s.Email.Contains(search));

            if (courseId.HasValue)
                q = q.Where(s => s.CourseId == courseId.Value);

            if (active.HasValue)
                q = q.Where(s => s.IsActive == active.Value);

            return await q.OrderBy(s => s.FirstName).ToListAsync();
        }

        public async Task<Student?> GetByIdAsync(int id)
            => await _db.Students
                .Include(s => s.Course).ThenInclude(c => c!.Department)
                .Include(s => s.Grades).ThenInclude(g => g.Subject)
                .Include(s => s.Attendances).ThenInclude(a => a.Subject)
                .Include(s => s.FeePayments)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<Student> CreateAsync(StudentViewModel vm, IFormFile? photo)
        {
            var student = new Student
            {
                FirstName       = vm.FirstName,
                LastName        = vm.LastName,
                Email           = vm.Email,
                PhoneNumber     = vm.PhoneNumber,
                DateOfBirth     = vm.DateOfBirth,
                Gender          = vm.Gender,
                Address         = vm.Address,
                CourseId        = vm.CourseId,
                AdmissionNumber = await GenerateAdmissionNumberAsync(),
                EnrollmentDate  = DateTime.Now,
            };

            if (photo != null)
                student.PhotoPath = await SavePhotoAsync(photo);

            _db.Students.Add(student);
            await _db.SaveChangesAsync();
            return student;
        }

        public async Task UpdateAsync(int id, StudentViewModel vm, IFormFile? photo)
        {
            var student = await _db.Students.FindAsync(id)
                ?? throw new KeyNotFoundException("Student not found");

            student.FirstName   = vm.FirstName;
            student.LastName    = vm.LastName;
            student.Email       = vm.Email;
            student.PhoneNumber = vm.PhoneNumber;
            student.DateOfBirth = vm.DateOfBirth;
            student.Gender      = vm.Gender;
            student.Address     = vm.Address;
            student.CourseId    = vm.CourseId;

            if (photo != null)
                student.PhotoPath = await SavePhotoAsync(photo);

            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var student = await _db.Students.FindAsync(id)
                ?? throw new KeyNotFoundException("Student not found");
            student.IsActive = false;   // soft delete
            await _db.SaveChangesAsync();
        }

        public async Task<StudentResultViewModel> GetResultsAsync(int studentId, string? semester = null)
        {
            var student = await GetByIdAsync(studentId)
                ?? throw new KeyNotFoundException("Student not found");

            var gradesQ = _db.Grades
                .Include(g => g.Subject)
                .Where(g => g.StudentId == studentId);

            if (!string.IsNullOrEmpty(semester))
                gradesQ = gradesQ.Where(g => g.Semester == semester);

            var grades = await gradesQ.ToListAsync();

            var rows = grades.Select(g => new GradeRowViewModel
            {
                SubjectName  = g.Subject?.Name ?? "",
                SubjectCode  = g.Subject?.Code ?? "",
                Credits      = g.Subject?.Credits ?? 0,
                Score        = g.Score,
                LetterGrade  = g.LetterGrade,
                Points       = g.Points,
                Semester     = g.Semester,
            }).ToList();

            var gpa = rows.Any()
                ? rows.Sum(r => r.Points * r.Credits) / Math.Max(rows.Sum(r => r.Credits), 1)
                : 0;

            return new StudentResultViewModel
            {
                Student  = student,
                Grades   = rows,
                GPA      = Math.Round(gpa, 2),
                GPAClass = gpa switch
                {
                    >= 3.6m => "First Class Honours",
                    >= 3.0m => "Second Class Upper",
                    >= 2.0m => "Second Class Lower",
                    >= 1.0m => "Pass",
                    _       => "Fail"
                },
            };
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private async Task<string> GenerateAdmissionNumberAsync()
        {
            var count = await _db.Students.CountAsync();
            return $"STU/{DateTime.Now.Year}/{count + 1:D4}";
        }

        private async Task<string> SavePhotoAsync(IFormFile photo)
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads", "students");
            Directory.CreateDirectory(uploads);
            var ext      = Path.GetExtension(photo.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploads, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await photo.CopyToAsync(stream);

            return $"/uploads/students/{fileName}";
        }
    }
}