using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    /// <summary>A member of teaching staff (professor, lecturer, TA).</summary>
    public class Instructor
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Title { get; set; } = "Dr.";

        [Required, MaxLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Phone, Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [MaxLength(50)]
        public string? Office { get; set; }

        [Display(Name = "Hire Date"), DataType(DataType.Date)]
        public DateTime HireDate { get; set; } = DateTime.Today;

        public bool IsActive { get; set; } = true;

        [Display(Name = "Department")]
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>The login account (AspNetUsers.Id) linked to this instructor, if any.</summary>
        public string? UserId { get; set; }

        public ICollection<Section> Sections { get; set; } = new List<Section>();

        public string FullName => $"{StudentManagement.Localization.L.T(Title)} {FirstName} {LastName}";

        public static readonly string[] Titles = { "Prof.", "Assoc. Prof.", "Asst. Prof.", "Dr.", "Lecturer", "TA" };
    }
}
