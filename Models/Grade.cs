namespace StudentManagement.Models
{
    public class Grade
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public Student? Student { get; set; }
        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }
        public int TermId { get; set; }
        public AcademicTerm? Term { get; set; }
        public decimal Score { get; set; }
        public string LetterGrade { get; set; } = string.Empty;
        public decimal Points { get; set; }

        public bool IsPassing => LetterGrade != "F";

        /// <summary>Credit-hour grading scale: minimum score, letter and grade points (4.0 scale).</summary>
        public static readonly (decimal Min, string Letter, decimal Points)[] Scale =
        {
            (90, "A",  4.0m),
            (85, "A-", 3.7m),
            (80, "B+", 3.3m),
            (75, "B",  3.0m),
            (70, "B-", 2.7m),
            (65, "C+", 2.3m),
            (60, "C",  2.0m),
            (50, "D",  1.0m),
            (0,  "F",  0.0m),
        };

        public static string ComputeLetter(decimal score) => Scale.First(s => score >= s.Min).Letter;

        public static decimal ComputePoints(decimal score) => Scale.First(s => score >= s.Min).Points;

        /// <summary>Bootstrap badge colour for a letter grade.</summary>
        public static string Badge(string letter) => letter.FirstOrDefault() switch
        {
            'A' => "bg-success",
            'B' => "bg-primary",
            'C' => "bg-info text-dark",
            'D' => "bg-warning text-dark",
            _   => "bg-danger",
        };
    }
}
