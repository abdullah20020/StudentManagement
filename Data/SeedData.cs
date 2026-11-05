using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Models;

namespace StudentManagement.Data
{
    /// <summary>
    /// Creates the database and fills it with a small sample university.
    ///
    /// Sample logins (development only):
    ///   Admin       admin@university.edu                    Admin@1234
    ///   Instructor  ahmed.hassan@university.edu (and every other instructor's email)   Instructor@1234
    ///   Student     mariam.ali@student.university.edu (and every other student's email) Student@1234
    /// </summary>
    public static class SeedData
    {
        public const string AdminRole = "Admin";
        public const string InstructorRole = "Instructor";
        public const string StudentRole = "Student";

        public const string InstructorPassword = "Instructor@1234";
        public const string StudentPassword = "Student@1234";

        public static async Task InitializeAsync(IServiceProvider services)
        {
            var db          = services.GetRequiredService<AppDbContext>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            // Create the database if needed and apply any pending EF Core migrations (see the Migrations folder).
            await db.Database.MigrateAsync();

            foreach (var role in new[] { AdminRole, InstructorRole, StudentRole })
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));

            await EnsureUser(userManager, "admin@university.edu", "Admin@1234", AdminRole);

            if (await db.Faculties.AnyAsync()) return;   // already seeded

            // ── Faculties & departments ─────────────────────────────────────
            var fcai = new Faculty { Name = "Faculty of Computers and Artificial Intelligence", Code = "FCAI", Dean = "Prof. Ahmed Hassan" };
            var com  = new Faculty { Name = "Faculty of Commerce", Code = "COM", Dean = "Prof. Hany Abdelaziz" };
            var eng  = new Faculty { Name = "Faculty of Engineering", Code = "ENG", Dean = "Prof. Tamer Salem" };
            db.Faculties.AddRange(fcai, com, eng);

            var cs  = new Department { Name = "Computer Science",       Code = "CS",  Faculty = fcai, HeadOfDepartment = "Dr. Mona Khalil" };
            var isd = new Department { Name = "Information Systems",    Code = "IS",  Faculty = fcai, HeadOfDepartment = "Dr. Sara Mahmoud" };
            var acc = new Department { Name = "Accounting",             Code = "ACC", Faculty = com,  HeadOfDepartment = "Prof. Hany Abdelaziz" };
            var ba  = new Department { Name = "Business Administration", Code = "BA", Faculty = com,  HeadOfDepartment = "Dr. Nadia Saleh" };
            var ee  = new Department { Name = "Electrical Engineering", Code = "EE",  Faculty = eng,  HeadOfDepartment = "Dr. Karim Mostafa" };
            db.Departments.AddRange(cs, isd, acc, ba, ee);

            // ── Programs (stored as Course) ─────────────────────────────────
            var bcs  = new Course { Name = "BSc Computer Science",       Code = "BCS",  Credits = 136, DurationYears = 4, Department = cs };
            var bis  = new Course { Name = "BSc Information Systems",    Code = "BIS",  Credits = 132, DurationYears = 4, Department = isd };
            var bacc = new Course { Name = "BCom Accounting",            Code = "BACC", Credits = 128, DurationYears = 4, Department = acc };
            var bee  = new Course { Name = "BSc Electrical Engineering", Code = "BEE",  Credits = 165, DurationYears = 5, Department = ee };
            db.Courses.AddRange(bcs, bis, bacc, bee);

            // ── Subjects with prerequisites ─────────────────────────────────
            var subjects = new Dictionary<string, Subject>();
            Subject Add(Course c, string code, string name, int year, int sem, int credits = 3, params string[] prereqs)
            {
                var s = new Subject { Code = code, Name = name, Course = c, YearOfStudy = year, Semester = $"Semester {sem}", Credits = credits };
                foreach (var p in prereqs) s.Prerequisites.Add(new SubjectPrerequisite { Subject = s, Prerequisite = subjects[p] });
                subjects[code] = s;
                db.Subjects.Add(s);
                return s;
            }

            Add(bcs, "CS101",   "Introduction to Programming", 1, 1);
            Add(bcs, "MATH101", "Calculus I",                  1, 1);
            Add(bcs, "CS102",   "Programming II",              1, 2, 3, "CS101");
            Add(bcs, "MATH102", "Discrete Mathematics",        1, 2);
            Add(bcs, "CS201",   "Data Structures",             2, 1, 3, "CS102");
            Add(bcs, "CS202",   "Database Systems",            2, 1, 3, "CS102");
            Add(bcs, "MATH201", "Linear Algebra",              2, 1, 3, "MATH101");
            Add(bcs, "CS203",   "Algorithms",                  2, 2, 3, "CS201", "MATH102");
            Add(bcs, "CS204",   "Web Development",             2, 2, 3, "CS202");

            Add(bis, "IS101", "Introduction to Information Systems", 1, 1);
            Add(bis, "IS102", "Programming Fundamentals",            1, 2, 3, "IS101");
            Add(bis, "IS201", "Systems Analysis and Design",         2, 1, 3, "IS101");
            Add(bis, "IS202", "Database Design",                     2, 1, 3, "IS102");

            Add(bacc, "ACC101", "Principles of Accounting I",  1, 1);
            Add(bacc, "ECO101", "Microeconomics",              1, 1);
            Add(bacc, "BUS101", "Business Mathematics",        1, 1, 2);
            Add(bacc, "ACC102", "Principles of Accounting II", 1, 2, 3, "ACC101");
            Add(bacc, "ACC201", "Cost Accounting",             2, 1, 3, "ACC102");

            Add(bee, "EE101",   "Electric Circuits I",         1, 1, 4);
            Add(bee, "MATH111", "Engineering Mathematics I",   1, 1);
            Add(bee, "EE102",   "Electric Circuits II",        1, 2, 4, "EE101");
            Add(bee, "EE201",   "Electronics I",               2, 1, 4, "EE102");

            // ── Instructors ─────────────────────────────────────────────────
            Instructor Teach(string title, string first, string last, Department d, string office) => new()
            {
                Title = title, FirstName = first, LastName = last, Department = d, Office = office,
                Email = $"{first.ToLower()}.{last.ToLower()}@university.edu",
                HireDate = new DateTime(2012 + first.Length, 9, 1),
            };
            var ahmed  = Teach("Prof.", "Ahmed",  "Hassan",     cs,  "FCAI 301");
            var mona   = Teach("Dr.",   "Mona",   "Khalil",     cs,  "FCAI 305");
            var omar   = Teach("Dr.",   "Omar",   "Farouk",     cs,  "FCAI 308");
            var sara   = Teach("Dr.",   "Sara",   "Mahmoud",    isd, "FCAI 210");
            var hany   = Teach("Prof.", "Hany",   "Abdelaziz",  acc, "COM 120");
            var nadia  = Teach("Dr.",   "Nadia",  "Saleh",      ba,  "COM 132");
            var karim  = Teach("Dr.",   "Karim",  "Mostafa",    ee,  "ENG 412");
            var yasmin = Teach("Dr.",   "Yasmin", "Adel",       ee,  "ENG 415");
            var instructors = new[] { ahmed, mona, omar, sara, hany, nadia, karim, yasmin };
            db.Instructors.AddRange(instructors);

            // ── Rooms ───────────────────────────────────────────────────────
            var a101 = new Room { Code = "A-101", Building = "Main Building", Capacity = 120, Type = "Lecture Hall" };
            var a102 = new Room { Code = "A-102", Building = "Main Building", Capacity = 80,  Type = "Lecture Hall" };
            var b201 = new Room { Code = "B-201", Building = "Building B",    Capacity = 45,  Type = "Classroom" };
            var b202 = new Room { Code = "B-202", Building = "Building B",    Capacity = 45,  Type = "Classroom" };
            var lab1 = new Room { Code = "LAB-1", Building = "FCAI Labs",     Capacity = 30,  Type = "Lab" };
            var lab2 = new Room { Code = "LAB-2", Building = "FCAI Labs",     Capacity = 30,  Type = "Lab" };
            db.Rooms.AddRange(a101, a102, b201, b202, lab1, lab2);

            // ── Terms ───────────────────────────────────────────────────────
            var fall25   = new AcademicTerm { Season = "Fall",   Year = 2025, StartDate = new(2025, 9, 20), EndDate = new(2026, 1, 15) };
            var spring26 = new AcademicTerm { Season = "Spring", Year = 2026, StartDate = new(2026, 2, 7),  EndDate = new(2026, 6, 10) };
            var fall26   = new AcademicTerm { Season = "Fall",   Year = 2026, StartDate = new(2026, 9, 19), EndDate = new(2027, 1, 14), IsCurrent = true, RegistrationOpen = true };
            db.Terms.AddRange(fall25, spring26, fall26);

            await db.SaveChangesAsync();

            // ── Fall 2026 sections and their weekly meetings (no clashes) ───
            var slots = new[] { (new TimeOnly(9, 0), new TimeOnly(10, 30)), (new TimeOnly(10, 45), new TimeOnly(12, 15)),
                                (new TimeOnly(12, 30), new TimeOnly(14, 0)), (new TimeOnly(14, 15), new TimeOnly(15, 45)) };
            var sections = new List<Section>();
            Section Offer(string code, string number, Instructor teacher, int capacity, params (DayOfWeek Day, int Slot, Room Room, string Type)[] meets)
            {
                var s = new Section { Subject = subjects[code], Term = fall26, Instructor = teacher, Number = number, Capacity = capacity };
                foreach (var m in meets)
                    s.Meetings.Add(new SectionMeeting { Day = m.Day, StartTime = slots[m.Slot - 1].Item1, EndTime = slots[m.Slot - 1].Item2, Room = m.Room, Type = m.Type });
                sections.Add(s);
                return s;
            }
            const DayOfWeek Sat = DayOfWeek.Saturday, Sun = DayOfWeek.Sunday, Mon = DayOfWeek.Monday,
                            Tue = DayOfWeek.Tuesday, Wed = DayOfWeek.Wednesday, Thu = DayOfWeek.Thursday;

            Offer("CS101",   "01", ahmed,  30, (Sat, 1, a102, "Lecture"), (Mon, 1, lab1, "Lab"));
            Offer("MATH101", "01", omar,   60, (Sun, 1, a102, "Lecture"), (Tue, 1, a102, "Lecture"));
            Offer("CS201",   "01", mona,   40, (Sat, 2, b201, "Lecture"), (Mon, 2, b201, "Lecture"));
            Offer("CS201",   "02", omar,   40, (Sun, 3, b202, "Lecture"), (Tue, 3, b202, "Lecture"));
            Offer("CS202",   "01", ahmed,  30, (Sun, 2, lab2, "Lecture"), (Wed, 2, lab2, "Lab"));
            Offer("MATH201", "01", omar,   60, (Mon, 3, a102, "Lecture"), (Wed, 3, a102, "Lecture"));
            Offer("IS101",   "01", sara,   40, (Sat, 3, b201, "Lecture"), (Tue, 2, b201, "Lecture"));
            Offer("IS201",   "01", sara,   40, (Mon, 4, b202, "Lecture"), (Wed, 1, b202, "Lecture"));
            Offer("IS202",   "01", mona,   30, (Thu, 1, lab1, "Lecture"), (Thu, 2, lab1, "Lab"));
            Offer("ACC101",  "01", hany,  100, (Sat, 1, a101, "Lecture"), (Tue, 1, a101, "Lecture"));
            Offer("ECO101",  "01", nadia, 100, (Sun, 1, a101, "Lecture"), (Wed, 1, a101, "Lecture"));
            Offer("BUS101",  "01", nadia, 100, (Sat, 2, a101, "Lecture"));
            Offer("ACC201",  "01", hany,  100, (Mon, 2, a101, "Lecture"), (Wed, 2, a101, "Lecture"));
            Offer("EE101",   "01", karim,  30, (Sat, 3, a102, "Lecture"), (Mon, 4, lab2, "Lab"));
            Offer("MATH111", "01", yasmin, 60, (Sun, 4, a102, "Lecture"), (Tue, 4, a102, "Lecture"));
            Offer("EE201",   "01", yasmin, 30, (Mon, 1, b202, "Lecture"), (Wed, 4, lab1, "Lab"));
            db.Sections.AddRange(sections);

            // ── Students ────────────────────────────────────────────────────
            // Profile: score range for past grades. Cohort 2025 has a year of grades; cohort 2026 is new this term.
            var random = new Random(42);
            var people = new (string First, string Last, string Gender, Course Program, int Cohort, int Min, int Max)[]
            {
                ("Youssef", "Ahmed",   "Male",   bcs,  2025, 82, 98),
                ("Mariam",  "Ali",     "Female", bcs,  2025, 66, 90),
                ("Omar",    "Khaled",  "Male",   bcs,  2025, 50, 64),   // weak: fails CS102, on probation
                ("Nour",    "Ibrahim", "Female", bcs,  2025, 60, 88),
                ("Hana",    "Mostafa", "Female", bcs,  2026, 0, 0),
                ("Ali",     "Samir",   "Male",   bcs,  2026, 0, 0),     // not registered yet
                ("Ahmed",   "Tarek",   "Male",   bis,  2025, 62, 85),
                ("Salma",   "Hossam",  "Female", bis,  2025, 84, 97),
                ("Mohamed", "Adel",    "Male",   bis,  2026, 0, 0),
                ("Farida",  "Nabil",   "Female", bacc, 2025, 80, 96),
                ("Mahmoud", "Fathy",   "Male",   bacc, 2025, 50, 66),
                ("Laila",   "Sherif",  "Female", bacc, 2026, 0, 0),
                ("Karim",   "Wael",    "Male",   bacc, 2026, 0, 0),
                ("Ziad",    "Hany",    "Male",   bee,  2025, 64, 88),
                ("Rana",    "Magdy",   "Female", bee,  2026, 0, 0),
                ("Hassan",  "Gamal",   "Male",   bee,  2026, 0, 0),
            };

            var students = new List<(Student Student, int Cohort, int Min, int Max)>();
            int seq = 1;
            foreach (var p in people)
            {
                var s = new Student
                {
                    FirstName = p.First,
                    LastName = p.Last,
                    Gender = p.Gender,
                    Course = p.Program,
                    Email = $"{p.First.ToLower()}.{p.Last.ToLower()}@student.university.edu",
                    PhoneNumber = $"01{random.Next(0, 3)}{random.Next(10000000, 99999999)}",
                    DateOfBirth = new DateTime(p.Cohort - 18, random.Next(1, 13), random.Next(1, 28)),
                    AdmissionNumber = $"{p.Cohort}{seq++:D4}",
                    EnrollmentDate = p.Cohort == 2025 ? fall25.StartDate : fall26.StartDate,
                    Address = new[] { "Nasr City, Cairo", "Dokki, Giza", "Heliopolis, Cairo", "Smouha, Alexandria", "Maadi, Cairo" }[random.Next(5)],
                };
                students.Add((s, p.Cohort, p.Min, p.Max));
            }
            db.Students.AddRange(students.Select(x => x.Student));
            await db.SaveChangesAsync();

            // ── Past grades for cohort 2025: year 1 semester 1 in Fall 2025, semester 2 in Spring 2026 ─
            var passed = new Dictionary<int, HashSet<int>>();
            foreach (var (s, cohort, min, max) in students.Where(x => x.Cohort == 2025))
            {
                passed[s.Id] = new HashSet<int>();
                foreach (var subj in subjects.Values.Where(x => x.CourseId == s.CourseId && x.YearOfStudy == 1))
                {
                    decimal score = random.Next(min, max + 1);
                    if (s.FirstName == "Omar" && subj.Code == "CS102") score = 42;   // a failed prerequisite
                    db.Grades.Add(new Grade
                    {
                        StudentId = s.Id,
                        SubjectId = subj.Id,
                        TermId = subj.Semester == "Semester 1" ? fall25.Id : spring26.Id,
                        Score = score,
                        LetterGrade = Grade.ComputeLetter(score),
                        Points = Grade.ComputePoints(score),
                    });
                    if (score >= 50) passed[s.Id].Add(subj.Id);
                }
            }

            // ── Fall 2026 registrations: year 2 sections for cohort 2025 (where prerequisites are met), year 1 for new students ─
            foreach (var (s, cohort, _, _) in students)
            {
                if (s.FirstName == "Ali") continue;   // left unregistered to try self-registration
                var year = cohort == 2025 ? 2 : 1;
                var taken = new HashSet<int>();
                foreach (var sec in sections.Where(x => x.Subject!.CourseId == s.CourseId && x.Subject.YearOfStudy == year))
                {
                    if (!taken.Add(sec.SubjectId)) continue;   // first section of each subject only
                    if (s.FirstName == "Mariam" && sec.Subject!.Code != "CS201") continue;   // leave room to register more
                    var prereqsMet = sec.Subject!.Prerequisites.All(p => passed.GetValueOrDefault(s.Id)?.Contains(p.PrerequisiteId) == true);
                    if (!prereqsMet) continue;
                    db.Registrations.Add(new Registration { StudentId = s.Id, SectionId = sec.Id, RegisteredOn = fall26.StartDate.AddDays(-7) });
                }
            }

            // ── Fees (EGP per semester) ─────────────────────────────────────
            int receipt = 1;
            var methods = new[] { "Cash", "Bank Transfer", "Card", "Fawry", "InstaPay" };
            foreach (var (s, cohort, _, _) in students)
            {
                var terms = cohort == 2025 ? new[] { fall25, spring26, fall26 } : new[] { fall26 };
                foreach (var t in terms)
                {
                    var isCurrent = t == fall26;
                    db.FeePayments.Add(new FeePayment
                    {
                        StudentId = s.Id,
                        ReceiptNumber = $"RCP/{t.StartDate.Year}/{receipt++:D4}",
                        Amount = isCurrent ? random.Next(8, 19) * 1000 : 18000,
                        Balance = 0,
                        Term = t.Season,
                        Year = t.Year,
                        PaymentMethod = methods[random.Next(methods.Length)],
                        TransactionReference = $"TX{random.Next(100000000, 999999999)}",
                        PaidOn = t.StartDate.AddDays(random.Next(-10, 15)),
                    });
                }
            }
            await db.SaveChangesAsync();

            // Current-term balance = 18,000 fee − what was paid.
            foreach (var f in db.FeePayments.Where(f => f.Term == "Fall" && f.Year == 2026))
                f.Balance = 18000 - f.Amount;
            await db.SaveChangesAsync();

            // ── Login accounts for every instructor and student ─────────────
            foreach (var i in instructors)
                i.UserId = await EnsureUser(userManager, i.Email, InstructorPassword, InstructorRole);
            foreach (var (s, _, _, _) in students)
                s.UserId = await EnsureUser(userManager, s.Email, StudentPassword, StudentRole);
            await db.SaveChangesAsync();
        }

        /// <summary>Creates the user (if missing) in the given role and returns its id.</summary>
        public static async Task<string?> EnsureUser(UserManager<IdentityUser> um, string email, string password, string role)
        {
            var user = await um.FindByEmailAsync(email);
            if (user == null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var result = await um.CreateAsync(user, password);
                if (!result.Succeeded) return null;
            }
            if (!await um.IsInRoleAsync(user, role))
                await um.AddToRoleAsync(user, role);
            return user.Id;
        }
    }
}
