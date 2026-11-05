using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    public class Course
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        public int Credits { get; set; } = 3;

        [MaxLength(500)]
        public string? Description { get; set; }

        public int DurationYears { get; set; } = 4;

        public bool IsActive { get; set; } = true;

        // FK
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        // Navigation
        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    }
}