#  Student Management System

> A full-featured student management web application built with **ASP.NET Core MVC**, **Entity Framework Core**, and **SQL Server**. Manage students, courses, grades, attendance, fees, and generate reports — all from a clean, role-based dashboard.

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?style=for-the-badge&logo=bootstrap&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

---

## 📋 Table of Contents

- [Features](#-features)
- [Screenshots](#-screenshots)
- [Tech Stack](#-tech-stack)
- [Prerequisites](#-prerequisites)
- [Getting Started](#-getting-started)
- [Project Structure](#-project-structure)
- [Database Schema](#-database-schema)
- [Configuration](#-configuration)
- [Running the App](#-running-the-app)
- [Default Credentials](#-default-credentials)
- [NuGet Packages](#-nuget-packages)
- [API Endpoints](#-api-endpoints)
- [Contributing](#-contributing)
- [License](#-license)

---

## ✨ Features

### 👨‍🎓 Student Management
- Register, edit, view and delete students
- Auto-generated admission numbers (`STU/2024/0001`)
- Photo upload support
- Search and filter by name, course, or admission number
- Paginated student listing

### 📚 Course & Department Management
- Create and manage departments
- Add courses with credits and assigned departments
- Enroll/unenroll students from courses

### 📝 Grades & Results
- Enter grades per subject per semester
- Automatic letter grade calculation (A, B, C, D, F)
- GPA computation per student
- Semester-wise result reports

### 📅 Attendance Tracking
- Mark daily attendance (Present / Absent / Late / Excused)
- Attendance summary per student
- Flag students below minimum attendance threshold

### 💰 Fee Management
- Record fee payments per student per term
- Track outstanding balances
- Payment history with receipt generation

### 📊 Reports & Exports
- PDF report generation (iTextSharp)
- Excel export (EPPlus)
- Student manifest per course
- Grade sheets per semester
- Printable fee statements

### 🔐 Authentication & Authorization
- ASP.NET Core Identity integration
- Role-based access: **Admin**, **Teacher**, **Student**
- Secure login / registration with email confirmation
- Password reset via email

### 🖥️ Dashboard
- Total students, courses, departments at a glance
- Recent registrations
- Attendance summary charts
- Fee collection overview

---

## 📸 Screenshots

```
screenshots/
├── dashboard.png
├── students-list.png
├── student-detail.png
├── grade-entry.png
└── reports.png
```

> _Add screenshots to the `screenshots/` folder and they will appear here._

---

## 🛠 Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8.0 MVC |
| ORM | Entity Framework Core 8.0 |
| Database | Microsoft SQL Server 2022 |
| Auth | ASP.NET Core Identity |
| Frontend | Razor Views + Bootstrap 5 |
| PDF | iTextSharp.LGPLv2.Core |
| Excel | EPPlus |
| Pagination | X.PagedList.Mvc.Core |
| Validation | FluentValidation.AspNetCore |
| Charting | Chart.js (CDN) |

---

## ✅ Prerequisites

Make sure the following are installed before you begin:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server 2019+](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) or [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended) **or** VS Code with the C# extension
- [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)

```bash
# Install EF Core CLI globally (if not already installed)
dotnet tool install --global dotnet-ef
```

---

## 🚀 Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/your-username/student-management-system.git
cd student-management-system
```

### 2. Restore NuGet packages

```bash
dotnet restore
```

### 3. Configure the database connection

Open `appsettings.json` and update the connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StudentManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

For SQL Server with credentials:

```json
"DefaultConnection": "Server=YOUR_SERVER;Database=StudentManagementDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

### 4. Apply database migrations

```bash
dotnet ef database update
```

This will create the database and all tables automatically.

### 5. (Optional) Seed sample data

```bash
dotnet run --seed
```

Or in Visual Studio, set `"SeedData": true` in `appsettings.Development.json`.

### 6. Run the application

```bash
dotnet run
```

Then open your browser at: **http://localhost:5000**

---

## 📁 Project Structure

```
StudentManagement/
│
├── Controllers/
│   ├── HomeController.cs           # Dashboard
│   ├── StudentsController.cs       # Student CRUD
│   ├── CoursesController.cs        # Course management
│   ├── GradesController.cs         # Grade entry & results
│   ├── AttendanceController.cs     # Attendance tracking
│   ├── FeesController.cs           # Fee management
│   └── ReportsController.cs        # PDF / Excel reports
│
├── Models/
│   ├── Student.cs
│   ├── Course.cs
│   ├── Department.cs
│   ├── Grade.cs
│   ├── Subject.cs
│   ├── Enrollment.cs
│   ├── Attendance.cs
│   └── FeePayment.cs
│
├── ViewModels/
│   ├── StudentViewModel.cs
│   ├── GradeViewModel.cs
│   └── DashboardViewModel.cs
│
├── Data/
│   ├── AppDbContext.cs
│   └── SeedData.cs
│
├── Services/
│   ├── StudentService.cs
│   ├── GradeService.cs
│   ├── ReportService.cs
│   └── EmailService.cs
│
├── Migrations/
│   └── (auto-generated)
│
├── Views/
│   ├── Home/
│   │   └── Index.cshtml            # Dashboard
│   ├── Students/
│   │   ├── Index.cshtml
│   │   ├── Create.cshtml
│   │   ├── Edit.cshtml
│   │   └── Details.cshtml
│   ├── Courses/
│   ├── Grades/
│   ├── Attendance/
│   ├── Fees/
│   └── Shared/
│       ├── _Layout.cshtml
│       └── _Navbar.cshtml
│
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── uploads/                    # Student photo uploads
│
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
└── StudentManagement.csproj
```

---

## 🗄 Database Schema

```
Students ──────── Enrollments ──────── Courses ──── Departments
    │                                      │
    ├── Grades ◄── Subjects ───────────────┘
    │
    ├── Attendances
    │
    └── FeePayments
```

### Key Tables

| Table | Description |
|---|---|
| `Students` | Core student records with admission number |
| `Courses` | Academic courses linked to departments |
| `Departments` | Faculty departments |
| `Enrollments` | Many-to-many: Students ↔ Courses |
| `Grades` | Score + letter grade per subject per semester |
| `Attendances` | Daily attendance records |
| `FeePayments` | Payment records with balance tracking |
| `AspNetUsers` | Identity: system users (admin, teacher, student) |
| `AspNetRoles` | Role definitions |

---

## ⚙️ Configuration

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "AppSettings": {
    "SchoolName": "Greenfield Academy",
    "AcademicYear": "2024/2025",
    "MinAttendancePercent": 75,
    "MaxPhotoSizeMB": 2,
    "AllowedPhotoExtensions": [ ".jpg", ".jpeg", ".png" ]
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "noreply@yourschool.com",
    "SenderName": "Student Portal",
    "EnableSsl": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## ▶️ Running the App

### Development

```bash
dotnet run
# or with hot reload
dotnet watch run
```

### Production build

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet StudentManagement.dll
```

### With Docker

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StudentManagement.dll"]
```

```bash
docker build -t student-management .
docker run -p 8080:80 student-management
```

---

## 🔑 Default Credentials

After seeding, the following accounts are available:

| Role | Email | Password |
|---|---|---|
| Admin | admin@school.com | Admin@1234 |
| Teacher | teacher@school.com | Teacher@1234 |
| Student | student@school.com | Student@1234 |

> ⚠️ **Change all default passwords immediately in production.**

---

## 📦 NuGet Packages

```bash
# ORM & database
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools

# Identity
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Identity.UI

# PDF generation
dotnet add package iTextSharp.LGPLv2.Core

# Excel export
dotnet add package EPPlus

# Pagination
dotnet add package X.PagedList.Mvc.Core

# Validation
dotnet add package FluentValidation.AspNetCore

# Email
dotnet add package MailKit
```

---

## 🔌 Key Routes

| Method | Route | Description |
|---|---|---|
| GET | `/` | Dashboard |
| GET | `/Students` | List all students |
| GET | `/Students/Create` | New student form |
| POST | `/Students/Create` | Register student |
| GET | `/Students/Edit/{id}` | Edit student |
| GET | `/Students/Details/{id}` | Student profile |
| POST | `/Students/Delete/{id}` | Delete student |
| GET | `/Courses` | List courses |
| GET | `/Grades/{studentId}` | Student grades |
| GET | `/Attendance` | Attendance sheet |
| GET | `/Fees/{studentId}` | Fee statement |
| GET | `/Reports/Students/pdf` | Download PDF report |
| GET | `/Reports/Students/excel` | Download Excel report |

---

## 🤝 Contributing

Contributions are welcome! Here's how:

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit your changes: `git commit -m "Add: your feature description"`
4. Push to your branch: `git push origin feature/your-feature`
5. Open a Pull Request

Please follow the existing code style and include XML doc comments on public methods.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgements

- [Microsoft ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core Docs](https://docs.microsoft.com/en-us/ef/core/)
- [Bootstrap 5](https://getbootstrap.com/)
- [Chart.js](https://www.chartjs.org/)
- [EPPlus](https://www.epplussoftware.com/)

---

<p align="center">Built with ❤️ using ASP.NET Core</p>