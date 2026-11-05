using static StudentManagement.Localization.L;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services
{
    /// <summary>A section a student could register for, and why not if they can't.</summary>
    public class Offering
    {
        public Section Section { get; init; } = null!;
        public int Enrolled { get; init; }
        public bool IsRegistered { get; init; }
        public int? RegistrationId { get; init; }
        public List<string> Problems { get; init; } = new();
        public bool CanRegister => !IsRegistered && Problems.Count == 0;
    }

    /// <summary>Everything about one student needed to check registration rules for a term.</summary>
    public class RegistrationContext
    {
        public Student Student { get; init; } = null!;
        public AcademicTerm Term { get; init; } = null!;
        public List<Grade> Grades { get; init; } = new();
        public List<Registration> Current { get; init; } = new();   // this term's registrations, with Section → Subject, Meetings
        public decimal? Cgpa { get; init; }
        public int MaxCredits { get; init; }
        public int CurrentCredits => Current.Sum(r => r.Section!.Subject!.Credits);
        public HashSet<int> PassedSubjectIds => Grades.Where(g => g.IsPassing).Select(g => g.SubjectId).ToHashSet();
    }

    /// <summary>Course registration: prerequisites, credit limits, capacity and timetable clashes.</summary>
    public class RegistrationService
    {
        private readonly AppDbContext _db;

        public RegistrationService(AppDbContext db) => _db = db;

        public async Task<RegistrationContext?> LoadAsync(int studentId, int termId)
        {
            var student = await _db.Students.Include(s => s.Course).FirstOrDefaultAsync(s => s.Id == studentId);
            var term = await _db.Terms.FindAsync(termId);
            if (student == null || term == null) return null;

            // Grades from terms before this one count for prerequisites and the CGPA.
            var grades = await _db.Grades
                .Include(g => g.Subject).Include(g => g.Term)
                .Where(g => g.StudentId == studentId && g.Term!.StartDate < term.StartDate)
                .ToListAsync();

            var current = await _db.Registrations
                .Include(r => r.Section!).ThenInclude(s => s.Subject)
                .Include(r => r.Section!).ThenInclude(s => s.Meetings).ThenInclude(m => m.Room)
                .Include(r => r.Section!).ThenInclude(s => s.Instructor)
                .Where(r => r.StudentId == studentId && r.Section!.TermId == termId)
                .ToListAsync();

            var cgpa = Academic.Cgpa(grades);
            return new RegistrationContext
            {
                Student = student,
                Term = term,
                Grades = grades,
                Current = current,
                Cgpa = cgpa,
                MaxCredits = Academic.MaxCredits(cgpa, term.Season),
            };
        }

        /// <summary>All sections of the student's program offered in the term, each with its eligibility.</summary>
        public async Task<List<Offering>> OfferingsAsync(RegistrationContext ctx, bool asAdmin)
        {
            var sections = await SectionsQuery()
                .Where(s => s.TermId == ctx.Term.Id && s.Subject!.CourseId == ctx.Student.CourseId)
                .OrderBy(s => s.Subject!.YearOfStudy).ThenBy(s => s.Subject!.Code).ThenBy(s => s.Number)
                .ToListAsync();
            var counts = await EnrolledCounts(sections.Select(s => s.Id));

            return sections.Select(s =>
            {
                var reg = ctx.Current.FirstOrDefault(r => r.SectionId == s.Id);
                return new Offering
                {
                    Section = s,
                    Enrolled = counts.GetValueOrDefault(s.Id),
                    IsRegistered = reg != null,
                    RegistrationId = reg?.Id,
                    Problems = reg != null ? new() : Check(ctx, s, counts.GetValueOrDefault(s.Id), asAdmin),
                };
            }).ToList();
        }

        /// <summary>Every reason the student can't register for this section; empty means they can.</summary>
        public List<string> Check(RegistrationContext ctx, Section section, int enrolled, bool asAdmin)
        {
            var problems = new List<string>();
            var subject = section.Subject!;

            if (!ctx.Term.RegistrationOpen && !asAdmin)
                problems.Add(T("Registration for {0} is closed.", ctx.Term.Name));
            if (!ctx.Student.IsActive)
                problems.Add(T("The student is inactive."));
            if (subject.CourseId != ctx.Student.CourseId)
                problems.Add(T("{0} isn't part of the student's program.", subject.Code));

            if (ctx.Current.Any(r => r.Section!.SubjectId == subject.Id))
                problems.Add(T("Already registered in another section of {0}.", subject.Code));
            if (ctx.PassedSubjectIds.Contains(subject.Id))
                problems.Add(T("{0} has already been passed.", subject.Code));

            var missing = subject.Prerequisites
                .Where(p => !ctx.PassedSubjectIds.Contains(p.PrerequisiteId))
                .Select(p => p.Prerequisite?.Code ?? "?")
                .ToList();
            if (missing.Any())
                problems.Add(T("Prerequisite not passed: {0}.", string.Join(", ", missing)));

            if (enrolled >= section.Capacity)
                problems.Add(T("Section is full ({0}/{1}).", enrolled, section.Capacity));

            foreach (var m in section.Meetings)
            {
                var clash = ctx.Current
                    .SelectMany(r => r.Section!.Meetings.Select(om => (Reg: r, Meeting: om)))
                    .FirstOrDefault(x => x.Meeting.Overlaps(m));
                if (clash.Reg != null)
                {
                    problems.Add(T("Time clash with {0} on {1} {2}.", $"{clash.Reg.Section!.Subject!.Code}-{clash.Reg.Section.Number}", Day(m.Day), clash.Meeting.TimeRange));
                    break;
                }
            }

            if (ctx.CurrentCredits + subject.Credits > ctx.MaxCredits)
                problems.Add((ctx.Cgpa < Academic.ProbationBelow
                                 ? T("Credit limit: {0} + {1} would exceed {2} credit hours (probation limit).", ctx.CurrentCredits, subject.Credits, ctx.MaxCredits)
                                 : T("Credit limit: {0} + {1} would exceed {2} credit hours.", ctx.CurrentCredits, subject.Credits, ctx.MaxCredits)));

            return problems;
        }

        /// <summary>Registers the student, or returns the reasons it isn't allowed.</summary>
        public async Task<(bool Ok, string Message)> RegisterAsync(int studentId, int sectionId, bool asAdmin)
        {
            var section = await SectionsQuery().FirstOrDefaultAsync(s => s.Id == sectionId);
            if (section == null) return (false, T("Section not found."));

            var ctx = await LoadAsync(studentId, section.TermId);
            if (ctx == null) return (false, T("Student not found."));

            var enrolled = await _db.Registrations.CountAsync(r => r.SectionId == sectionId);
            var problems = Check(ctx, section, enrolled, asAdmin);
            if (problems.Any()) return (false, string.Join(" ", problems));

            _db.Registrations.Add(new Registration { StudentId = studentId, SectionId = sectionId });
            await _db.SaveChangesAsync();
            return (true, T("{0} registered in {1}.", ctx.Student.FullName, $"{section.Subject!.Code}-{section.Number}"));
        }

        public async Task<(bool Ok, string Message)> DropAsync(int registrationId, bool asAdmin)
        {
            var reg = await _db.Registrations
                .Include(r => r.Section!).ThenInclude(s => s.Subject)
                .Include(r => r.Section!).ThenInclude(s => s.Term)
                .FirstOrDefaultAsync(r => r.Id == registrationId);
            if (reg == null) return (false, T("Registration not found."));
            if (!reg.Section!.Term!.RegistrationOpen && !asAdmin)
                return (false, T("Registration for {0} is closed, so sections can't be dropped.", reg.Section.Term.Name));

            _db.Registrations.Remove(reg);
            await _db.SaveChangesAsync();
            return (true, T("Dropped {0}.", $"{reg.Section.Subject!.Code}-{reg.Section.Number}"));
        }

        public IQueryable<Section> SectionsQuery() => _db.Sections
            .Include(s => s.Subject!).ThenInclude(s => s.Prerequisites).ThenInclude(p => p.Prerequisite)
            .Include(s => s.Instructor)
            .Include(s => s.Term)
            .Include(s => s.Meetings).ThenInclude(m => m.Room)
            .AsSplitQuery();

        public async Task<Dictionary<int, int>> EnrolledCounts(IEnumerable<int> sectionIds)
        {
            var ids = sectionIds.ToList();
            return await _db.Registrations
                .Where(r => ids.Contains(r.SectionId))
                .GroupBy(r => r.SectionId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
        }

        public async Task<AcademicTerm?> CurrentTermAsync() =>
            await _db.Terms.FirstOrDefaultAsync(t => t.IsCurrent)
            ?? await _db.Terms.OrderByDescending(t => t.StartDate).FirstOrDefaultAsync();
    }
}
