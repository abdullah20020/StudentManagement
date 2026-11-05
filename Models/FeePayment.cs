namespace StudentManagement.Models
{
    public class FeePayment
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public Student? Student { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public string Term { get; set; } = string.Empty;
        public int Year { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? TransactionReference { get; set; }
        public DateTime PaidOn { get; set; } = DateTime.Now;
    }
}
