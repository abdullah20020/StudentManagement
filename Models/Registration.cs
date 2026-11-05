namespace StudentManagement.Models
{
    /// <summary>A student registered in a section for a term. Dropping a section deletes the row.</summary>
    public class Registration
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int SectionId { get; set; }
        public Section? Section { get; set; }

        public DateTime RegisteredOn { get; set; } = DateTime.Now;
    }
}
