using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnpaidLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HalfDayPart",
                table: "LeaveRequests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHalfDay",
                table: "LeaveRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UnpaidDays",
                table: "LeaveRequests",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HalfDayPart",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "IsHalfDay",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "UnpaidDays",
                table: "LeaveRequests");
        }
    }
}
