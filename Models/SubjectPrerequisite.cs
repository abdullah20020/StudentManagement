namespace StudentManagement.Models
{
    /// <summary>Subject <see cref="SubjectId"/> needs a passing grade in <see cref="PrerequisiteId"/> first.</summary>
    public class SubjectPrerequisite
    {
        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        public int PrerequisiteId { get; set; }
        public Subject? Prerequisite { get; set; }
    }
}
