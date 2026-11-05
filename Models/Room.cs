using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    /// <summary>A lecture hall, classroom or lab where sections meet.</summary>
    public class Room
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Building { get; set; } = string.Empty;

        [Range(1, 1000)]
        public int Capacity { get; set; } = 40;

        [Required, MaxLength(20)]
        public string Type { get; set; } = "Classroom";

        public ICollection<SectionMeeting> Meetings { get; set; } = new List<SectionMeeting>();

        public static readonly string[] Types = { "Lecture Hall", "Classroom", "Lab" };
    }
}
