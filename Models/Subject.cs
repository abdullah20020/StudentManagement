using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    public class Subject
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        /// <summary>Credit hours.</summary>
        public int Credits { get; set; } = 3;

        public int YearOfStudy { get; set; } = 1;

        [MaxLength(20)]
        public string Semester { get; set; } = "Semester 1";

        // FK
        public int CourseId { get; set; }
        public Course? Course { get; set; }

        // Navigation
        public ICollection<Grade> Grades { get; set; } = new List<Grade>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<Section> Sections { get; set; } = new List<Section>();

        /// <summary>Subjects that must be passed before this one.</summary>
        public ICollection<SubjectPrerequisite> Prerequisites { get; set; } = new List<SubjectPrerequisite>();

        /// <summary>Subjects that list this one as a prerequisite.</summary>
        public ICollection<SubjectPrerequisite> RequiredBy { get; set; } = new List<SubjectPrerequisite>();
    }
}
