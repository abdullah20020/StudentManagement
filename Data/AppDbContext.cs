using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Models;

namespace StudentManagement.Data
{
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Student>             Students             { get; set; }
        public DbSet<Course>              Courses              { get; set; }
        public DbSet<Department>          Departments          { get; set; }
        public DbSet<Faculty>             Faculties            { get; set; }
        public DbSet<Instructor>          Instructors          { get; set; }
        public DbSet<Subject>             Subjects             { get; set; }
        public DbSet<SubjectPrerequisite> SubjectPrerequisites { get; set; }
        public DbSet<AcademicTerm>        Terms                { get; set; }
        public DbSet<Room>                Rooms                { get; set; }
        public DbSet<Section>             Sections             { get; set; }
        public DbSet<SectionMeeting>      SectionMeetings      { get; set; }
        public DbSet<Registration>        Registrations        { get; set; }
        public DbSet<Grade>               Grades               { get; set; }
        public DbSet<Enrollment>          Enrollments          { get; set; }
        public DbSet<Attendance>          Attendances          { get; set; }
        public DbSet<FeePayment>          FeePayments          { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Unique admission number
            builder.Entity<Student>()
                .HasIndex(s => s.AdmissionNumber)
                .IsUnique();

            // Unique course (program) code
            builder.Entity<Course>()
                .HasIndex(c => c.Code)
                .IsUnique();

            builder.Entity<Faculty>()
                .HasIndex(f => f.Code)
                .IsUnique();

            builder.Entity<Room>()
                .HasIndex(r => r.Code)
                .IsUnique();

            builder.Entity<Instructor>()
                .HasIndex(i => i.Email)
                .IsUnique();

            builder.Entity<AcademicTerm>()
                .HasIndex(t => new { t.Season, t.Year })
                .IsUnique();

            // Student → Course (many-to-one)
            builder.Entity<Student>()
                .HasOne(s => s.Course)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Faculty → Department, Department → Instructor: block deletes while children exist
            builder.Entity<Department>()
                .HasOne(d => d.Faculty)
                .WithMany(f => f.Departments)
                .HasForeignKey(d => d.FacultyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Instructor>()
                .HasOne(i => i.Department)
                .WithMany(d => d.Instructors)
                .HasForeignKey(i => i.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prerequisites: a self-referencing many-to-many on Subject
            builder.Entity<SubjectPrerequisite>()
                .HasKey(p => new { p.SubjectId, p.PrerequisiteId });
            builder.Entity<SubjectPrerequisite>()
                .HasOne(p => p.Subject)
                .WithMany(s => s.Prerequisites)
                .HasForeignKey(p => p.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<SubjectPrerequisite>()
                .HasOne(p => p.Prerequisite)
                .WithMany(s => s.RequiredBy)
                .HasForeignKey(p => p.PrerequisiteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Sections
            builder.Entity<Section>()
                .HasIndex(s => new { s.SubjectId, s.TermId, s.Number })
                .IsUnique();
            builder.Entity<Section>()
                .HasOne(s => s.Instructor)
                .WithMany(i => i.Sections)
                .HasForeignKey(s => s.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Section>()
                .HasOne(s => s.Term)
                .WithMany(t => t.Sections)
                .HasForeignKey(s => s.TermId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SectionMeeting>()
                .HasOne(m => m.Room)
                .WithMany(r => r.Meetings)
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // Registration: a student can be in a section only once
            builder.Entity<Registration>()
                .HasIndex(r => new { r.StudentId, r.SectionId })
                .IsUnique();

            // Enrollment composite uniqueness
            builder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.CourseId })
                .IsUnique();

            // Grade: one grade per student per subject per term
            builder.Entity<Grade>()
                .HasIndex(g => new { g.StudentId, g.SubjectId, g.TermId })
                .IsUnique();
            builder.Entity<Grade>()
                .HasOne(g => g.Term)
                .WithMany()
                .HasForeignKey(g => g.TermId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Grade>()
                .Property(g => g.LetterGrade)
                .HasMaxLength(2);

            // Attendance: one record per student per subject per date
            builder.Entity<Attendance>()
                .HasIndex(a => new { a.StudentId, a.SubjectId, a.Date })
                .IsUnique();

            // Decimal precision
            builder.Entity<Grade>()
                .Property(g => g.Score)
                .HasColumnType("decimal(5,2)");

            builder.Entity<Grade>()
                .Property(g => g.Points)
                .HasColumnType("decimal(3,2)");

            builder.Entity<FeePayment>()
                .Property(f => f.Amount)
                .HasColumnType("decimal(12,2)");

            builder.Entity<FeePayment>()
                .Property(f => f.Balance)
                .HasColumnType("decimal(12,2)");
        }
    }
}
