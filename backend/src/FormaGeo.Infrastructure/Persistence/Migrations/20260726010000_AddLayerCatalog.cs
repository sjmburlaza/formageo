using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FormaGeo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>Adds the contextual layer catalog and project preferences.</summary>
    public partial class AddLayerCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    license_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    license_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    attribution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "layer_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geographic_coverage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    coordinate_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    geometry_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    feature_name_property = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    style_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_layer_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_layer_definitions_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "layer_legends",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    layer_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fill_color = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    stroke_color = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    symbol = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_layer_legends", x => x.id);
                    table.ForeignKey(
                        name: "FK_layer_legends_layer_definitions_layer_definition_id",
                        column: x => x.layer_definition_id,
                        principalTable: "layer_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "layer_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    layer_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivery_method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    data_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    source_layer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    minimum_zoom = table.Column<int>(type: "integer", nullable: true),
                    maximum_zoom = table.Column<int>(type: "integer", nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_layer_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_layer_versions_layer_definitions_layer_definition_id",
                        column: x => x.layer_definition_id,
                        principalTable: "layer_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_layers",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    layer_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    opacity = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    filter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_layers", x => new { x.project_id, x.layer_definition_id });
                    table.ForeignKey(
                        name: "FK_project_layers_layer_definitions_layer_definition_id",
                        column: x => x.layer_definition_id,
                        principalTable: "layer_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_layers_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "data_sources",
                columns: new[] { "id", "attribution", "license_name", "license_url", "name", "organization", "source_url" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), "FormaGeo demonstration data — illustrative only", "CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/", "FormaGeo contextual demonstration data", "FormaGeo", null });

            migrationBuilder.InsertData(
                table: "layer_definitions",
                columns: new[] { "id", "category", "coordinate_system", "data_source_id", "description", "feature_name_property", "geographic_coverage", "geometry_type", "is_active", "name", "style_json" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Boundaries", "EPSG:4326", new Guid("10000000-0000-0000-0000-000000000001"), "Illustrative administrative boundaries for testing overlay workflows.", "name", "Metro Manila demonstration extent", "Polygon", true, "Planning districts", "{\"fillColor\":\"#6366f1\",\"strokeColor\":\"#3730a3\",\"strokeWidth\":2}" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "Planning", "EPSG:4326", new Guid("10000000-0000-0000-0000-000000000001"), "Illustrative generalized land-use areas for contextual analysis.", "name", "Metro Manila demonstration extent", "Polygon", true, "Land-use zones", "{\"fillColor\":\"#f59e0b\",\"strokeColor\":\"#b45309\",\"strokeWidth\":1.5}" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "Hazards", "EPSG:4326", new Guid("10000000-0000-0000-0000-000000000001"), "Illustrative flood susceptibility areas; not suitable for risk decisions.", "name", "Metro Manila demonstration extent", "Polygon", true, "Flood susceptibility", "{\"fillColor\":\"#0ea5e9\",\"strokeColor\":\"#0369a1\",\"strokeWidth\":1.5}" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "Environment", "EPSG:4326", new Guid("10000000-0000-0000-0000-000000000001"), "Illustrative environmental areas for testing planning overlays.", "name", "Metro Manila demonstration extent", "Polygon", true, "Green and protected areas", "{\"fillColor\":\"#22c55e\",\"strokeColor\":\"#15803d\",\"strokeWidth\":1.5}" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "Transport", "EPSG:4326", new Guid("10000000-0000-0000-0000-000000000001"), "Illustrative transport links for testing line overlays.", "name", "Metro Manila demonstration extent", "LineString", true, "Primary transport corridors", "{\"lineColor\":\"#ef4444\",\"lineWidth\":3}" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), "Facilities", "EPSG:4326", new Guid("10000000-0000-0000-0000-000000000001"), "Illustrative facility locations for testing point overlays and identification.", "name", "Metro Manila demonstration extent", "Point", true, "Community facilities", "{\"circleColor\":\"#8b5cf6\",\"circleRadius\":7,\"strokeColor\":\"#ffffff\",\"strokeWidth\":2}" }
                });

            migrationBuilder.InsertData(
                table: "layer_legends",
                columns: new[] { "id", "fill_color", "label", "layer_definition_id", "sort_order", "stroke_color", "symbol" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "#6366f1", "District boundary", new Guid("20000000-0000-0000-0000-000000000001"), 0, "#3730a3", null },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "#f59e0b", "Mixed-use zone", new Guid("20000000-0000-0000-0000-000000000002"), 0, "#b45309", null },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "#0ea5e9", "Moderate susceptibility", new Guid("20000000-0000-0000-0000-000000000003"), 0, "#0369a1", null },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "#22c55e", "Green / protected area", new Guid("20000000-0000-0000-0000-000000000004"), 0, "#15803d", null },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "#ef4444", "Primary corridor", new Guid("20000000-0000-0000-0000-000000000005"), 0, "#991b1b", null },
                    { new Guid("40000000-0000-0000-0000-000000000006"), "#8b5cf6", "Community facility", new Guid("20000000-0000-0000-0000-000000000006"), 0, "#ffffff", null }
                });

            migrationBuilder.InsertData(
                table: "layer_versions",
                columns: new[] { "id", "data_url", "delivery_method", "is_current", "last_updated_at_utc", "layer_definition_id", "maximum_zoom", "minimum_zoom", "source_layer", "version_label" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), "/layers/planning-districts.geojson", "GeoJson", true, new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000001"), null, null, null, "2026.07-demo" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "/layers/land-use-zones.geojson", "GeoJson", true, new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000002"), null, null, null, "2026.07-demo" },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "/layers/flood-susceptibility.geojson", "GeoJson", true, new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000003"), null, null, null, "2026.07-demo" },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "/layers/green-areas.geojson", "GeoJson", true, new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000004"), null, null, null, "2026.07-demo" },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "/layers/transport-corridors.geojson", "GeoJson", true, new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000005"), null, null, null, "2026.07-demo" },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "/layers/community-facilities.geojson", "GeoJson", true, new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000006"), null, null, null, "2026.07-demo" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_layer_definitions_category",
                table: "layer_definitions",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_layer_definitions_data_source_id",
                table: "layer_definitions",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_layer_definitions_is_active",
                table: "layer_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_layer_legends_layer_definition_id_sort_order",
                table: "layer_legends",
                columns: new[] { "layer_definition_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_layer_versions_layer_definition_id_is_current",
                table: "layer_versions",
                columns: new[] { "layer_definition_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "IX_project_layers_layer_definition_id",
                table: "project_layers",
                column: "layer_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_layers_project_id_sort_order",
                table: "project_layers",
                columns: new[] { "project_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "layer_legends");

            migrationBuilder.DropTable(
                name: "layer_versions");

            migrationBuilder.DropTable(
                name: "project_layers");

            migrationBuilder.DropTable(
                name: "layer_definitions");

            migrationBuilder.DropTable(
                name: "data_sources");
        }
    }
}
