using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrantTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaskTitleLengthAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_Title_Length",
                table: "Tasks",
                sql: "length(\"Title\") <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_Title_Length",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tasks");
        }
    }
}
