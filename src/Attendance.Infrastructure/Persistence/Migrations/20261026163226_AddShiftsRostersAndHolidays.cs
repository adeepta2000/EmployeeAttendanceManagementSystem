using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftsRostersAndHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DefaultShiftId",
                table: "Employees",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WeeklyOffPatternId",
                table: "Employees",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CrossesMidnight = table.Column<bool>(type: "bit", nullable: false),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    GraceInMinutes = table.Column<int>(type: "int", nullable: false),
                    GraceOutMinutes = table.Column<int>(type: "int", nullable: false),
                    HalfDayThresholdMinutes = table.Column<int>(type: "int", nullable: false),
                    FullDayMinutes = table.Column<int>(type: "int", nullable: false),
                    PunchWindowBeforeMinutes = table.Column<int>(type: "int", nullable: false),
                    PunchWindowAfterMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.CheckConstraint("CK_Shifts_CrossesMidnight", "[CrossesMidnight] = CASE WHEN [EndTime] < [StartTime] THEN 1 ELSE 0 END");
                    table.CheckConstraint("CK_Shifts_StartTime_EndTime", "[StartTime] <> [EndTime]");
                });

            migrationBuilder.CreateTable(
                name: "WeeklyOffPatterns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyOffPatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayLocations",
                columns: table => new
                {
                    HolidayId = table.Column<long>(type: "bigint", nullable: false),
                    WorkLocationId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayLocations", x => new { x.HolidayId, x.WorkLocationId });
                    table.ForeignKey(
                        name: "FK_HolidayLocations_Holidays_HolidayId",
                        column: x => x.HolidayId,
                        principalTable: "Holidays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HolidayLocations_WorkLocations_WorkLocationId",
                        column: x => x.WorkLocationId,
                        principalTable: "WorkLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeShiftRosters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    RosterDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftId = table.Column<long>(type: "bigint", nullable: false),
                    IsWeeklyOffOverride = table.Column<bool>(type: "bit", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeShiftRosters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftRosters_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftRosters_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyOffPatternDays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatternId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    WeekOfMonth = table.Column<byte>(type: "tinyint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyOffPatternDays", x => x.Id);
                    table.CheckConstraint("CK_WeeklyOffPatternDays_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_WeeklyOffPatternDays_WeekOfMonth", "[WeekOfMonth] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_WeeklyOffPatternDays_WeeklyOffPatterns_PatternId",
                        column: x => x.PatternId,
                        principalTable: "WeeklyOffPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DefaultShiftId",
                table: "Employees",
                column: "DefaultShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_WeeklyOffPatternId",
                table: "Employees",
                column: "WeeklyOffPatternId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftRosters_EmployeeId_RosterDate",
                table: "EmployeeShiftRosters",
                columns: new[] { "EmployeeId", "RosterDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftRosters_PublicId",
                table: "EmployeeShiftRosters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftRosters_ShiftId",
                table: "EmployeeShiftRosters",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Roster_Date",
                table: "EmployeeShiftRosters",
                column: "RosterDate");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayLocations_WorkLocationId",
                table: "HolidayLocations",
                column: "WorkLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_HolidayDate_Name",
                table: "Holidays",
                columns: new[] { "HolidayDate", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_PublicId",
                table: "Holidays",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Code",
                table: "Shifts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_PublicId",
                table: "Shifts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyOffPatternDays_PatternId_DayOfWeek_WeekOfMonth",
                table: "WeeklyOffPatternDays",
                columns: new[] { "PatternId", "DayOfWeek", "WeekOfMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyOffPatterns_Name",
                table: "WeeklyOffPatterns",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyOffPatterns_PublicId",
                table: "WeeklyOffPatterns",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Shifts_DefaultShiftId",
                table: "Employees",
                column: "DefaultShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_WeeklyOffPatterns_WeeklyOffPatternId",
                table: "Employees",
                column: "WeeklyOffPatternId",
                principalTable: "WeeklyOffPatterns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Shifts_DefaultShiftId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_WeeklyOffPatterns_WeeklyOffPatternId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "EmployeeShiftRosters");

            migrationBuilder.DropTable(
                name: "HolidayLocations");

            migrationBuilder.DropTable(
                name: "WeeklyOffPatternDays");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "WeeklyOffPatterns");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DefaultShiftId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_WeeklyOffPatternId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DefaultShiftId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "WeeklyOffPatternId",
                table: "Employees");
        }
    }
}
