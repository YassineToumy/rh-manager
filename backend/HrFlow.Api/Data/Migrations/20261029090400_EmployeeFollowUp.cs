using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ChipCredit",
                table: "EmployeeCompensations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChipNumber",
                table: "EmployeeCompensations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkMissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ManagerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkMissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkMissions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkMissions_EmployeeId_WeekStart",
                table: "WorkMissions",
                columns: new[] { "EmployeeId", "WeekStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkMissions");

            migrationBuilder.DropColumn(
                name: "ChipCredit",
                table: "EmployeeCompensations");

            migrationBuilder.DropColumn(
                name: "ChipNumber",
                table: "EmployeeCompensations");
        }
    }
}
