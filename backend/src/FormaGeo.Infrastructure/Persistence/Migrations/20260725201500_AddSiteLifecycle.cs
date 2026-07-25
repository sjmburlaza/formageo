using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormaGeo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at_utc",
                table: "sites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "sites",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "sites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE sites
                SET status = 'Active',
                    updated_at_utc = created_at_utc
                """);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "sites",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "sites",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "status",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "sites");
        }
    }
}
