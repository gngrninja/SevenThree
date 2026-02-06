using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenThree.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionArchiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_TestId",
                table: "Questions");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastImportedAt",
                table: "Questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TestId_QuestionSection",
                table: "Questions",
                columns: new[] { "TestId", "QuestionSection" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_TestId_QuestionSection",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "LastImportedAt",
                table: "Questions");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TestId",
                table: "Questions",
                column: "TestId");
        }
    }
}
