using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azka.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEmailToEngineer : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Engineers",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Email",
            table: "Engineers");
    }
}
