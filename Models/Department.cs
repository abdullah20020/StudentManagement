using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Code { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Head of Department")]
        public string? HeadOfDepartment { get; set; }

        public bool IsActive { get; set; } = true;

        // FK
        [Display(Name = "Faculty")]
        public int? FacultyId { get; set; }
        public Faculty? Faculty { get; set; }

        // Navigation
        public ICollection<Course> Courses { get; set; } = new List<Course>();
        public ICollection<Instructor> Instructors { get; set; } = new List<Instructor>();
    }
}
