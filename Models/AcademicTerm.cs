using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    /// <summary>A semester such as "Fall 2026". Sections, registrations and grades all belong to a term.</summary>
    public class AcademicTerm
    {
        public int Id { get; set; }

        [Required, MaxLength(10)]
        public string Season { get; set; } = "Fall";

        [Range(2000, 2100)]
        public int Year { get; set; } = DateTime.Today.Year;

        [Display(Name = "Start Date"), DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "End Date"), DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(4);

        /// <summary>The term the university is in now. Exactly one term should be current.</summary>
        [Display(Name = "Current term")]
        public bool IsCurrent { get; set; }

        /// <summary>Students can only add or drop sections while this is on.</summary>
        [Display(Name = "Registration open")]
        public bool RegistrationOpen { get; set; }

        public ICollection<Section> Sections { get; set; } = new List<Section>();

        /// <summary>"Fall 2026", or "خريف 2026" when the interface is in Arabic.</summary>
        public string Name => $"{StudentManagement.Localization.L.T(Season)} {Year}";

        public static readonly string[] Seasons = { "Fall", "Spring", "Summer" };
    }
}
