using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormaGeo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteComparisons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_comparisons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scoring_scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scoring_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    comparison_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_comparisons", x => x.id);
                    table.ForeignKey(
                        name: "FK_site_comparisons_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_site_comparisons_scoring_models_scoring_model_id",
                        column: x => x.scoring_model_id,
                        principalTable: "scoring_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_comparisons_scoring_scenarios_scoring_scenario_id",
                        column: x => x.scoring_scenario_id,
                        principalTable: "scoring_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_comparisons_project_id_comparison_date_utc",
                table: "site_comparisons",
                columns: new[] { "project_id", "comparison_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_site_comparisons_scoring_model_id",
                table: "site_comparisons",
                column: "scoring_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_site_comparisons_scoring_scenario_id",
                table: "site_comparisons",
                column: "scoring_scenario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_comparisons");
        }
    }
}
