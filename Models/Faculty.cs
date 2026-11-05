using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    /// <summary>A faculty (college) of the university, e.g. Faculty of Engineering. Departments sit under it.</summary>
    public class Faculty
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Dean { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Department> Departments { get; set; } = new List<Department>();
    }
}
