using StudentManagement.Models;

namespace StudentManagement.Services
{
    /// <summary>One term on a transcript: the grades earned that term plus the running totals after it.</summary>
    public class TermRecord
    {
        public AcademicTerm Term { get; init; } = null!;
        public List<Grade> Grades { get; init; } = new();
        public int TermCredits { get; init; }
        public decimal? TermGpa { get; init; }
        public int CumulativeCredits { get; init; }
        public int EarnedCredits { get; init; }
        public decimal? Cgpa { get; init; }
    }

    /// <summary>GPA, academic standing and credit-load rules, kept in one place.</summary>
    public static class Academic
    {
        public const decimal ProbationBelow = 2.0m;
        public const decimal HonorsFrom = 3.5m;

        /// <summary>
        /// Builds a transcript, oldest term first. Grades must have Subject and Term loaded.
        /// When a subject is retaken, only the latest attempt counts towards the CGPA.
        /// </summary>
        public static List<TermRecord> Transcript(IEnumerable<Grade> grades)
        {
            var latest = new Dictionary<int, Grade>();   // subjectId → most recent attempt so far
            var records = new List<TermRecord>();

            foreach (var term in grades.GroupBy(g => g.TermId)
                                       .Select(g => (Term: g.First().Term!, Grades: g.OrderBy(x => x.Subject!.Code).ToList()))
                                       .OrderBy(t => t.Term.StartDate))
            {
                foreach (var g in term.Grades) latest[g.SubjectId] = g;

                var termCredits = term.Grades.Sum(g => g.Subject!.Credits);
                var cumCredits = latest.Values.Sum(g => g.Subject!.Credits);
                records.Add(new TermRecord
                {
                    Term = term.Term,
                    Grades = term.Grades,
                    TermCredits = termCredits,
                    TermGpa = Gpa(term.Grades),
                    CumulativeCredits = cumCredits,
                    EarnedCredits = latest.Values.Where(g => g.IsPassing).Sum(g => g.Subject!.Credits),
                    Cgpa = Gpa(latest.Values),
                });
            }
            return records;
        }

        /// <summary>Credit-weighted GPA of the given grades, or null if there are no credits.</summary>
        public static decimal? Gpa(IEnumerable<Grade> grades)
        {
            var list = grades.ToList();
            var credits = list.Sum(g => g.Subject!.Credits);
            return credits == 0 ? null : Math.Round(list.Sum(g => g.Points * g.Subject!.Credits) / credits, 2);
        }

        public static decimal? Cgpa(IEnumerable<Grade> grades) => Transcript(grades).LastOrDefault()?.Cgpa;

        public static int EarnedCredits(IEnumerable<Grade> grades) => Transcript(grades).LastOrDefault()?.EarnedCredits ?? 0;

        public static string Standing(decimal? cgpa) => cgpa switch
        {
            null => "New student",
            < ProbationBelow => "Academic probation",
            >= HonorsFrom => "Honors",
            _ => "Good standing",
        };

        public static string StandingBadge(decimal? cgpa) => cgpa switch
        {
            null => "bg-secondary",
            < ProbationBelow => "bg-danger",
            >= HonorsFrom => "bg-success",
            _ => "bg-primary",
        };

        /// <summary>Maximum credit hours a student may register for in one term.</summary>
        public static int MaxCredits(decimal? cgpa, string season)
        {
            if (season == "Summer") return 9;
            return cgpa switch
            {
                null => 18,
                < ProbationBelow => 12,
                >= HonorsFrom => 21,
                _ => 18,
            };
        }

        /// <summary>Study level from earned credits: every 33 credits moves the student up a level.</summary>
        public static int Level(int earnedCredits) => Math.Min(5, 1 + earnedCredits / 33);
    }
}
