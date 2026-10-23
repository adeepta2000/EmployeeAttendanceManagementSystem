using Attendance.Domain.Organization;

namespace Attendance.Infrastructure.Persistence.Seeding.Demo;

/// <summary>
/// Generates the demo dataset for the fictional Nordwin Textiles Ltd. Everything in it is invented
/// (CLAUDE.md data conventions), and it is deterministic: the same date and seed always give the same
/// company, so every developer and every demo sees the same people.
/// </summary>
/// <remarks>
/// <para>
/// The org chart is three levels deep. The General Manager is at the top; eight managers report to them
/// (the HR Manager and seven line managers); everyone else reports to one of those eight.
/// </para>
/// <para>
/// The personas of spec §1.2 are real people here, so a demo can sign in as them: Ms. Farhana Rahman
/// (HR Manager), Tanvir Ahmed (HR Officer), Nusrat Jahan (sewing floor manager, Dhaka) and Rafiq Islam
/// (sewing operator in Nusrat's team). Everyone else gets a generated name, mostly Bengali with some English.
/// </para>
/// </remarks>
public static class NordwinDemoData
{
    public const string EmailDomain = "nordwin.example";
    public const int DefaultSeed = 20_261_013;

    public static DemoDataSet Generate(DateOnly asOf, int seed = DefaultSeed) => new Builder(asOf, seed).Build();

    private enum Seniority
    {
        TopManagement,
        Management,
        Staff,
    }

    private sealed record PersonName(string First, string Last, Gender Gender);

    private sealed class Builder(DateOnly asOf, int seed)
    {
        private readonly Random _random = new(seed);
        private readonly HashSet<string> _fullNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _emails = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<DemoEmployee> _employees = [];

        private readonly Dictionary<string, Department> _departments = new[]
        {
            new Department { Code = "ADM", Name = "Administration" },
            new Department { Code = "HR", Name = "Human Resources & Compliance" },
            new Department { Code = "CUT", Name = "Cutting" },
            new Department { Code = "SEW", Name = "Sewing" },
            new Department { Code = "FIN", Name = "Finishing & Packing" },
            new Department { Code = "QA", Name = "Quality Assurance" },
        }.ToDictionary(d => d.Code);

        private readonly Dictionary<string, Designation> _designations = new[]
        {
            new Designation { Code = "GM", Name = "General Manager", Grade = "M-1" },
            new Designation { Code = "MGR", Name = "Manager", Grade = "M-2" },
            new Designation { Code = "EXEC", Name = "Executive", Grade = "S-1" },
            new Designation { Code = "HRO", Name = "HR Officer", Grade = "S-1" },
            new Designation { Code = "SUP", Name = "Line Supervisor", Grade = "S-2" },
            new Designation { Code = "TECH", Name = "Maintenance Technician", Grade = "W-2" },
            new Designation { Code = "QI", Name = "Quality Inspector", Grade = "W-2" },
            new Designation { Code = "CTM", Name = "Cutting Master", Grade = "W-2" },
            new Designation { Code = "SOP", Name = "Senior Sewing Operator", Grade = "W-3" },
            new Designation { Code = "OPR", Name = "Sewing Operator", Grade = "W-4" },
            new Designation { Code = "FOP", Name = "Finishing Operator", Grade = "W-4" },
            new Designation { Code = "HLP", Name = "Helper", Grade = "W-5" },
        }.ToDictionary(d => d.Code);

        // Addresses are deliberately generic; the coordinates are the two city centres.
        private readonly Dictionary<string, WorkLocation> _locations = new[]
        {
            new WorkLocation
            {
                Code = "DHK", Name = "Dhaka Unit", Address = "Plot 7, Nordwin Industrial Park, Dhaka",
                Latitude = 23.810331m, Longitude = 90.412521m, GeofenceRadiusMeters = 300,
            },
            new WorkLocation
            {
                Code = "CTG", Name = "Chittagong Unit", Address = "Plot 3, Nordwin Industrial Park, Chittagong",
                Latitude = 22.356851m, Longitude = 91.783182m, GeofenceRadiusMeters = 300,
            },
        }.ToDictionary(l => l.Code);

        private readonly Dictionary<Department, Employee> _heads = [];

        public DemoDataSet Build()
        {
            // Reserved first, so a generated name can never take a persona's name or email.
            _fullNames.UnionWith(["Farhana Rahman", "Tanvir Ahmed", "Nusrat Jahan", "Rafiq Islam"]);

            // Level 1
            var gm = Add(null, "ADM", "GM", "DHK", manager: null, SystemRoles.Manager, Seniority.TopManagement);
            _heads[_departments["ADM"]] = gm;

            // Level 2: the eight managers
            var adminIt = Add(null, "ADM", "MGR", "DHK", gm, SystemRoles.Manager, Seniority.Management);
            var maintenance = Add(null, "ADM", "MGR", "DHK", gm, SystemRoles.Manager, Seniority.Management);
            var hr = Add(new("Farhana", "Rahman", Gender.Female), "HR", "MGR", "DHK", gm, SystemRoles.HRManager, Seniority.Management);
            var cutting = Add(null, "CUT", "MGR", "DHK", gm, SystemRoles.Manager, Seniority.Management);
            var sewingDhaka = Add(new("Nusrat", "Jahan", Gender.Female), "SEW", "MGR", "DHK", gm, SystemRoles.Manager, Seniority.Management);
            var sewingChittagong = Add(null, "SEW", "MGR", "CTG", gm, SystemRoles.Manager, Seniority.Management);
            var finishing = Add(null, "FIN", "MGR", "CTG", gm, SystemRoles.Manager, Seniority.Management);
            var quality = Add(null, "QA", "MGR", "DHK", gm, SystemRoles.Manager, Seniority.Management);

            _heads[_departments["HR"]] = hr;
            _heads[_departments["CUT"]] = cutting;
            _heads[_departments["SEW"]] = sewingDhaka;
            _heads[_departments["FIN"]] = finishing;
            _heads[_departments["QA"]] = quality;

            // Level 3: the teams
            Add(new("Tanvir", "Ahmed", Gender.Male), "HR", "HRO", "DHK", hr, SystemRoles.HROfficer, Seniority.Staff);
            Add(null, "HR", "HRO", "DHK", hr, SystemRoles.HROfficer, Seniority.Staff);
            Team(hr, "HR", _ => "DHK", ("EXEC", 2));
            Add(null, "HR", "EXEC", "DHK", hr, SystemRoles.Employee, Seniority.Staff, EmploymentType.Intern);

            Team(adminIt, "ADM", _ => "DHK", ("EXEC", 4), ("HLP", 1));
            Add(null, "ADM", "EXEC", "DHK", adminIt, SystemRoles.Employee, Seniority.Staff, EmploymentType.Intern);

            Team(maintenance, "ADM", i => i % 2 == 0 ? "DHK" : "CTG", ("TECH", 7), ("HLP", 1));
            Team(cutting, "CUT", _ => "DHK", ("SUP", 1), ("CTM", 6), ("HLP", 9));

            Add(new("Rafiq", "Islam", Gender.Male), "SEW", "OPR", "DHK", sewingDhaka, SystemRoles.Employee, Seniority.Staff);
            Team(sewingDhaka, "SEW", _ => "DHK", ("SUP", 2), ("SOP", 6), ("OPR", 11), ("HLP", 4));
            Team(sewingChittagong, "SEW", _ => "CTG", ("SUP", 2), ("SOP", 6), ("OPR", 12), ("HLP", 4));
            Team(finishing, "FIN", _ => "CTG", ("SUP", 1), ("FOP", 11), ("HLP", 4));
            Team(quality, "QA", i => i % 3 == 2 ? "CTG" : "DHK", ("SUP", 1), ("QI", 11));

            AssignEmployeeCodes();

            // A few leavers, so the active filter and FR-1.5 have something to show.
            Separate(sewingDhaka, "OPR");
            Separate(finishing, "FOP");
            Separate(cutting, "HLP");

            return new DemoDataSet(
                [.. _departments.Values],
                [.. _designations.Values],
                [.. _locations.Values],
                _employees,
                _heads);
        }

        private void Team(Employee manager, string department, Func<int, string> location, params (string Designation, int Count)[] members)
        {
            var index = 0;
            foreach (var (designation, count) in members)
            {
                for (var i = 0; i < count; i++, index++)
                {
                    Add(null, department, designation, location(index), manager, SystemRoles.Employee, Seniority.Staff);
                }
            }
        }

        private Employee Add(
            PersonName? name,
            string department,
            string designation,
            string location,
            Employee? manager,
            string role,
            Seniority seniority,
            EmploymentType? employmentType = null)
        {
            if (name is null)
            {
                name = RandomName(designation, seniority);
                _fullNames.Add($"{name.First} {name.Last}");
            }

            var joiningDate = employmentType == EmploymentType.Intern
                ? asOf.AddDays(-_random.Next(14, 120))
                : seniority switch
                {
                    Seniority.TopManagement => asOf.AddYears(-14).AddDays(-_random.Next(0, 120)),
                    Seniority.Management => asOf.AddYears(-_random.Next(5, 12)).AddDays(-_random.Next(0, 365)),
                    _ => asOf.AddDays(-_random.Next(30, 12 * 365)),
                };
            var ageAtJoining = seniority switch
            {
                Seniority.TopManagement => 38,
                Seniority.Management => _random.Next(28, 41),
                _ => employmentType == EmploymentType.Intern ? _random.Next(21, 25) : _random.Next(18, 35),
            };

            var type = employmentType ?? DefaultEmploymentType(designation, joiningDate);
            var employee = new Employee
            {
                EmployeeCode = string.Empty,
                FirstName = name.First,
                LastName = name.Last,
                DateOfBirth = joiningDate.AddYears(-ageAtJoining).AddDays(-_random.Next(0, 365)),
                Gender = name.Gender,
                Email = NewEmail(name),
                JoiningDate = joiningDate,
                ConfirmationDate = type == EmploymentType.Permanent ? joiningDate.AddMonths(6) : null,
                EmploymentType = type,
                Department = _departments[department],
                Designation = _designations[designation],
                WorkLocation = _locations[location],
                ReportingManager = manager,
            };

            _employees.Add(new DemoEmployee(employee, role));
            return employee;
        }

        private EmploymentType DefaultEmploymentType(string designation, DateOnly joiningDate)
        {
            if (joiningDate > asOf.AddMonths(-6))
            {
                return EmploymentType.Probation;
            }

            // Helpers and technicians are often on fixed-term contracts.
            return designation is "HLP" or "TECH" && _random.NextDouble() < 0.3
                ? EmploymentType.Contract
                : EmploymentType.Permanent;
        }

        // Codes follow joining order, as an HR desk would have issued them.
        private void AssignEmployeeCodes()
        {
            var ordered = _employees.Select((e, index) => (e.Employee, index))
                .OrderBy(e => e.Employee.JoiningDate)
                .ThenBy(e => e.index)
                .Select(e => e.Employee);

            var number = 0;
            foreach (var employee in ordered)
            {
                employee.EmployeeCode = $"NW-{++number:D3}";
            }
        }

        private void Separate(Employee manager, string designation)
        {
            var leaver = _employees.Select(e => e.Employee)
                .First(e => e.ReportingManager == manager
                    && e.Designation.Code == designation
                    && e.IsActive
                    && e.JoiningDate < asOf.AddMonths(-6)
                    && e.FirstName != "Rafiq");
            leaver.Deactivate(asOf.AddDays(-_random.Next(10, 90)));
        }

        private PersonName RandomName(string designation, Seniority seniority)
        {
            var femaleShare = seniority == Seniority.Staff && designation is "SOP" or "OPR" or "FOP" or "HLP" or "QI" ? 0.6 : 0.3;
            var gender = _random.NextDouble() < femaleShare ? Gender.Female : Gender.Male;

            for (var attempt = 0; ; attempt++)
            {
                var roll = _random.NextDouble();
                var name = roll switch
                {
                    < 0.68 => Pick(gender, Names.MuslimMale, Names.MuslimFemale, Names.MuslimSurnames, Names.MuslimFemaleSurnames),
                    < 0.83 => Pick(gender, Names.HinduMale, Names.HinduFemale, Names.HinduSurnames, []),
                    _ => Pick(gender, Names.EnglishMale, Names.EnglishFemale, Names.EnglishSurnames, []),
                };

                if (!_fullNames.Contains($"{name.First} {name.Last}") || attempt >= 20)
                {
                    return name;
                }
            }
        }

        private PersonName Pick(Gender gender, string[] male, string[] female, string[] surnames, string[] femaleSurnames)
        {
            var first = gender == Gender.Female ? female[_random.Next(female.Length)] : male[_random.Next(male.Length)];
            var lastPool = gender == Gender.Female && femaleSurnames.Length > 0 && _random.NextDouble() < 0.5 ? femaleSurnames : surnames;
            return new PersonName(first, lastPool[_random.Next(lastPool.Length)], gender);
        }

        private string NewEmail(PersonName name)
        {
            static string Part(string s) => new string([.. s.Where(char.IsAsciiLetter)]).ToLowerInvariant();

            var local = $"{Part(name.First)}.{Part(name.Last)}";
            var email = $"{local}@{EmailDomain}";
            for (var n = 2; !_emails.Add(email); n++)
            {
                email = $"{local}{n}@{EmailDomain}";
            }

            return email;
        }
    }

    private static class Names
    {
        public static readonly string[] MuslimMale =
        [
            "Karim", "Jahangir", "Mizanur", "Habibur", "Shahidul", "Kamrul", "Arif", "Sohel", "Rasel", "Masud",
            "Faruk", "Nazrul", "Alamgir", "Monir", "Shafiqul", "Rubel", "Saiful", "Imran", "Jewel", "Anwar",
            "Delwar", "Mamun", "Shakil", "Riyad", "Sabbir", "Tarek", "Hasan", "Mahbub", "Ashraf", "Zahid",
        ];

        public static readonly string[] MuslimFemale =
        [
            "Shirin", "Taslima", "Rokeya", "Sumaiya", "Nasrin", "Mitu", "Rupa", "Shapla", "Moushumi", "Ayesha",
            "Fatema", "Jannatul", "Sharmin", "Tania", "Lipi", "Rina", "Shathi", "Sabina", "Mahmuda", "Nazma",
            "Kulsum", "Rehana", "Jesmin", "Salma", "Morium", "Popy", "Rumana", "Afroza", "Nadia", "Laboni",
        ];

        public static readonly string[] MuslimSurnames =
        [
            "Islam", "Rahman", "Ahmed", "Hossain", "Chowdhury", "Uddin", "Miah", "Sarker", "Sheikh", "Talukder",
            "Haque", "Alam", "Molla", "Bhuiyan", "Khan", "Mahmud", "Kabir", "Siddique",
        ];

        public static readonly string[] MuslimFemaleSurnames = ["Akter", "Begum", "Khatun", "Sultana", "Parvin", "Jahan"];

        public static readonly string[] HinduMale = ["Pradip", "Shyamal", "Subrata", "Tapan", "Gopal", "Ratan", "Sujit", "Bishwajit"];

        public static readonly string[] HinduFemale = ["Mitali", "Rupali", "Shampa", "Puja", "Anjali", "Dipa", "Sraboni", "Kakoli"];

        public static readonly string[] HinduSurnames = ["Das", "Saha", "Roy", "Dey", "Paul", "Ghosh", "Sen", "Mondal", "Barua"];

        public static readonly string[] EnglishMale =
            ["James", "Oliver", "Daniel", "Thomas", "William", "Henry", "Edward", "George", "Michael", "David"];

        public static readonly string[] EnglishFemale =
            ["Emily", "Charlotte", "Sophie", "Olivia", "Grace", "Hannah", "Lucy", "Amelia", "Rachel", "Laura"];

        public static readonly string[] EnglishSurnames =
        [
            "Carter", "Whitmore", "Bennett", "Hughes", "Walker", "Turner", "Collins", "Morgan", "Fletcher",
            "Harrison", "Reed", "Palmer",
        ];
    }
}

/// <param name="DepartmentHeads">Set after the first save: departments and employees reference each other.</param>
public sealed record DemoDataSet(
    IReadOnlyList<Department> Departments,
    IReadOnlyList<Designation> Designations,
    IReadOnlyList<WorkLocation> WorkLocations,
    IReadOnlyList<DemoEmployee> Employees,
    IReadOnlyDictionary<Department, Employee> DepartmentHeads);

/// <param name="Role">The system role of the employee's login account.</param>
public sealed record DemoEmployee(Employee Employee, string Role);
