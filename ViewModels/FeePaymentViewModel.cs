using System.ComponentModel.DataAnnotations;

namespace StudentManagement.ViewModels
{
    /// <summary>Form model for recording a fee payment.</summary>
    public class FeePaymentViewModel
    {
        [Required(ErrorMessage = "Select a student.")]
        [Display(Name = "Student")]
        public int? StudentId { get; set; }

        [Required, Range(0.01, 100_000_000, ErrorMessage = "Amount must be more than 0.")]
        [Display(Name = "Amount paid")]
        public decimal? Amount { get; set; }

        [Required, Range(0, 100_000_000, ErrorMessage = "Balance can't be negative.")]
        [Display(Name = "Balance remaining")]
        public decimal? Balance { get; set; }

        [Required, MaxLength(20)]
        public string Term { get; set; } = "Fall";

        [Required, Range(2000, 2100)]
        public int Year { get; set; } = DateTime.Now.Year;

        [Required, MaxLength(30)]
        [Display(Name = "Payment method")]
        public string PaymentMethod { get; set; } = "Cash";

        [MaxLength(100)]
        [Display(Name = "Transaction reference")]
        public string? TransactionReference { get; set; }

        [Required, DataType(DataType.Date)]
        [Display(Name = "Paid on")]
        public DateTime PaidOn { get; set; } = DateTime.Today;

        public string? ReturnUrl { get; set; }

        /// <summary>Fees are charged per semester, matching AcademicTerm.Seasons.</summary>
        public static readonly string[] Terms = { "Fall", "Spring", "Summer" };
        public static readonly string[] Methods = { "Cash", "Bank Transfer", "Card", "Fawry", "InstaPay" };
    }
}
