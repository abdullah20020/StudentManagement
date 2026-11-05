namespace StudentManagement.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public Student? Student { get; set; }
        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "Present"; // Present / Absent / Late / Excused

        public static readonly string[] Statuses = { "Present", "Late", "Absent", "Excused" };

        /// <summary>
        /// Attendance rate in percent: Present and Late count as attended, Excused days are left out.
        /// Null when there's nothing to count.
        /// </summary>
        public static decimal? Rate(int present, int late, int absent)
        {
            var counted = present + late + absent;
            return counted == 0 ? null : Math.Round(100m * (present + late) / counted, 1);
        }
    }
}
