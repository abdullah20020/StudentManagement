using System.ComponentModel.DataAnnotations;

namespace StudentManagement.ViewModels
{
    /// <summary>Form model for entering or editing a grade.</summary>
    public class GradeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Select a student.")]
        [Display(Name = "Student")]
        public int? StudentId { get; set; }

        [Required(ErrorMessage = "Select a subject.")]
        [Display(Name = "Subject")]
        public int? SubjectId { get; set; }

        [Required, Range(0, 100, ErrorMessage = "Score must be between 0 and 100.")]
        public decimal? Score { get; set; }

        [Required(ErrorMessage = "Select a term.")]
        [Display(Name = "Term")]
        public int? TermId { get; set; }

        /// <summary>Local URL to go back to after saving (e.g. the student's page).</summary>
        public string? ReturnUrl { get; set; }
    }
}
