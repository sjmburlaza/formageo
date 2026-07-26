using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormaGeo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generated_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comparison_id = table.Column<Guid>(type: "uuid", nullable: true),
                    format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sections = table.Column<string>(type: "jsonb", nullable: false),
                    branding = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_generated_reports_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generated_reports_comparison_id_generated_at_utc",
                table: "generated_reports",
                columns: new[] { "comparison_id", "generated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_reports_project_id_generated_at_utc",
                table: "generated_reports",
                columns: new[] { "project_id", "generated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_reports_site_id_generated_at_utc",
                table: "generated_reports",
                columns: new[] { "site_id", "generated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_reports_storage_key",
                table: "generated_reports",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_reports");
        }
    }
}
