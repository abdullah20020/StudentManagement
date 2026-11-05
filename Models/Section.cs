using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    /// <summary>One offering of a subject in a term (e.g. CS201 section 02, Fall 2026), with its instructor and weekly meetings.</summary>
    public class Section
    {
        public int Id { get; set; }

        [Display(Name = "Subject")]
        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        [Display(Name = "Term")]
        public int TermId { get; set; }
        public AcademicTerm? Term { get; set; }

        [Display(Name = "Instructor")]
        public int? InstructorId { get; set; }
        public Instructor? Instructor { get; set; }

        [Required, MaxLength(10)]
        [Display(Name = "Section")]
        public string Number { get; set; } = "01";

        [Range(1, 1000)]
        public int Capacity { get; set; } = 40;

        public ICollection<SectionMeeting> Meetings { get; set; } = new List<SectionMeeting>();
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

        public string Label => $"{Subject?.Code}-{Number}";
    }
}
