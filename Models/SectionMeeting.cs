namespace StudentManagement.Models
{
    /// <summary>A weekly slot when a section meets, e.g. Sunday 10:45–12:15 in B-201.</summary>
    public class SectionMeeting
    {
        public int Id { get; set; }

        public int SectionId { get; set; }
        public Section? Section { get; set; }

        public DayOfWeek Day { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        public int? RoomId { get; set; }
        public Room? Room { get; set; }

        public string Type { get; set; } = "Lecture";

        public static readonly string[] Types = { "Lecture", "Lab", "Tutorial" };

        /// <summary>The teaching week, Saturday to Thursday.</summary>
        public static readonly DayOfWeek[] TeachingDays =
        {
            DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday,
            DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        };

        public bool Overlaps(SectionMeeting other) =>
            Day == other.Day && StartTime < other.EndTime && other.StartTime < EndTime;

        public string TimeRange => $"{StartTime:HH\\:mm}–{EndTime:HH\\:mm}";
    }
}
