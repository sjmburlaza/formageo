using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormaGeo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSuitabilityScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scoring_scenarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_scenarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_scoring_scenarios_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scoring_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scoring_scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_scoring_models_scoring_scenarios_scoring_scenario_id",
                        column: x => x.scoring_scenario_id,
                        principalTable: "scoring_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scoring_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scoring_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterion_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    direction = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    normalization_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    data_source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lower_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    upper_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    missing_data_behavior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_criteria", x => x.id);
                    table.ForeignKey(
                        name: "FK_scoring_criteria_scoring_models_scoring_model_id",
                        column: x => x.scoring_model_id,
                        principalTable: "scoring_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scoring_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scoring_scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scoring_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overall_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    rating = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_scoreable = table.Column<bool>(type: "boolean", nullable: false),
                    breakdown = table.Column<string>(type: "jsonb", nullable: false),
                    calculated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_scoring_results_scoring_models_scoring_model_id",
                        column: x => x.scoring_model_id,
                        principalTable: "scoring_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scoring_results_scoring_scenarios_scoring_scenario_id",
                        column: x => x.scoring_scenario_id,
                        principalTable: "scoring_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scoring_results_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scoring_criteria_scoring_model_id_criterion_key",
                table: "scoring_criteria",
                columns: new[] { "scoring_model_id", "criterion_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scoring_models_scoring_scenario_id_version",
                table: "scoring_models",
                columns: new[] { "scoring_scenario_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scoring_results_scoring_model_id",
                table: "scoring_results",
                column: "scoring_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_results_scoring_scenario_id_site_id_calculated_at_u~",
                table: "scoring_results",
                columns: new[] { "scoring_scenario_id", "site_id", "calculated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_scoring_results_site_id",
                table: "scoring_results",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_scenarios_project_id",
                table: "scoring_scenarios",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scoring_criteria");

            migrationBuilder.DropTable(
                name: "scoring_results");

            migrationBuilder.DropTable(
                name: "scoring_models");

            migrationBuilder.DropTable(
                name: "scoring_scenarios");
        }
    }
}
