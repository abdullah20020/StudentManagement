namespace StudentManagement.ViewModels
{
    /// <summary>The class register for one section on one date.</summary>
    public class AttendanceSheetViewModel
    {
        public int? SectionId { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public List<AttendanceRow> Rows { get; set; } = new();

        // Display only
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public string? CourseName { get; set; }
    }

    public class AttendanceRow
    {
        public int StudentId { get; set; }

        /// <summary>Present / Late / Absent / Excused, or empty for "not marked".</summary>
        public string? Status { get; set; }

        // Display only
        public string? StudentName { get; set; }
        public string? AdmissionNumber { get; set; }
    }

    /// <summary>One student's attendance totals, for the summary page.</summary>
    public class AttendanceSummaryRow
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string AdmissionNumber { get; set; } = string.Empty;
        public string? CourseName { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public int Excused { get; set; }
        public int Total => Present + Late + Absent + Excused;
        public decimal? Rate { get; set; }
    }
}
