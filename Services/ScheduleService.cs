using static StudentManagement.Localization.L;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services
{
    /// <summary>One block on a weekly timetable.</summary>
    public class TimetableEntry
    {
        public DayOfWeek Day { get; init; }
        public TimeOnly Start { get; init; }
        public TimeOnly End { get; init; }
        public string Title { get; init; } = "";
        public string? Subtitle { get; init; }
        public string? Room { get; init; }
        public string? Type { get; init; }
        public int SectionId { get; init; }
    }

    /// <summary>Clash checks for rooms and instructors, and timetable building.</summary>
    public class ScheduleService
    {
        private readonly AppDbContext _db;

        public ScheduleService(AppDbContext db) => _db = db;

        /// <summary>Reasons the meeting can't be scheduled (room or instructor double-booked, room too small, bad times).</summary>
        public async Task<List<string>> CheckMeetingAsync(Section section, SectionMeeting meeting)
        {
            var problems = new List<string>();
            if (meeting.EndTime <= meeting.StartTime)
                problems.Add(T("The end time must be after the start time."));
            if (!SectionMeeting.TeachingDays.Contains(meeting.Day))
                problems.Add(T("Classes run Saturday to Thursday."));
            if (problems.Any()) return problems;

            var others = await _db.SectionMeetings
                .Include(m => m.Section!).ThenInclude(s => s.Subject)
                .Include(m => m.Section!).ThenInclude(s => s.Instructor)
                .Include(m => m.Room)
                .Where(m => m.Id != meeting.Id && m.Section!.TermId == section.TermId && m.Day == meeting.Day)
                .ToListAsync();

            foreach (var o in others.Where(o => o.Overlaps(meeting)))
            {
                var what = $"{o.Section!.Subject!.Code}-{o.Section.Number} ({Day(o.Day)} {o.TimeRange})";
                if (o.SectionId == section.Id)
                    problems.Add(T("This section already meets then: {0}.", what));
                else if (meeting.RoomId.HasValue && o.RoomId == meeting.RoomId)
                    problems.Add(T("Room {0} is already booked by {1}.", o.Room?.Code, what));
                else if (section.InstructorId.HasValue && o.Section.InstructorId == section.InstructorId)
                    problems.Add(T("{0} is already teaching {1}.", o.Section.Instructor?.FullName, what));
            }

            if (meeting.RoomId.HasValue)
            {
                var room = await _db.Rooms.FindAsync(meeting.RoomId.Value);
                if (room == null) problems.Add(T("Room not found."));
                else if (room.Capacity < section.Capacity)
                    problems.Add(T("Room {0} holds {1}, but the section capacity is {2}.", room.Code, room.Capacity, section.Capacity));
            }
            return problems;
        }

        /// <summary>Clashes the instructor would have if they took over this section's meetings.</summary>
        public async Task<List<string>> CheckInstructorAsync(Section section, int instructorId)
        {
            var mine = await _db.SectionMeetings.Where(m => m.SectionId == section.Id).ToListAsync();
            if (!mine.Any()) return new();

            var theirs = await _db.SectionMeetings
                .Include(m => m.Section!).ThenInclude(s => s.Subject)
                .Where(m => m.SectionId != section.Id && m.Section!.TermId == section.TermId && m.Section.InstructorId == instructorId)
                .ToListAsync();

            return (from a in mine
                    from b in theirs
                    where a.Overlaps(b)
                    select T("The instructor already teaches {0} on {1} {2}.", $"{b.Section!.Subject!.Code}-{b.Section.Number}", Day(b.Day), b.TimeRange))
                   .Distinct().ToList();
        }

        public static List<TimetableEntry> Entries(IEnumerable<Section> sections, bool showInstructor = true) =>
            sections.SelectMany(s => s.Meetings.Select(m => new TimetableEntry
            {
                Day = m.Day,
                Start = m.StartTime,
                End = m.EndTime,
                Title = $"{s.Subject?.Code}-{s.Number}",
                Subtitle = showInstructor ? s.Instructor?.FullName ?? T("No instructor") : s.Subject?.Name,
                Room = m.Room?.Code,
                Type = m.Type,
                SectionId = s.Id,
            })).OrderBy(e => e.Start).ToList();
    }
}
