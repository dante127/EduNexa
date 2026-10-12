using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SchoolStructureCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "structure");

            migrationBuilder.CreateTable(
                name: "academic_years",
                schema: "structure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_academic_years", x => x.id);
                    table.CheckConstraint("ck_academic_years_dates", "end_date > start_date");
                    table.CheckConstraint("ck_academic_years_status", "status IN ('Planned', 'Active', 'Closed')");
                });

            migrationBuilder.CreateTable(
                name: "classrooms",
                schema: "structure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_classrooms", x => x.id);
                    table.CheckConstraint("ck_classrooms_capacity", "capacity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "grade_levels",
                schema: "structure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_levels", x => x.id);
                    table.CheckConstraint("ck_grade_levels_sequence", "sequence > 0");
                });

            migrationBuilder.CreateTable(
                name: "schools",
                schema: "structure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    default_locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    logo_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schools", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                schema: "structure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_en = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "academic_terms",
                schema: "structure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_academic_terms", x => x.id);
                    table.CheckConstraint("ck_academic_terms_dates", "end_date > start_date");
                    table.CheckConstraint("ck_academic_terms_sequence", "sequence > 0");
                    table.CheckConstraint("ck_academic_terms_status", "status IN ('Planned', 'Active', 'Closed')");
                    table.ForeignKey(
                        name: "fk_academic_terms_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalSchema: "structure",
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_sections",
                schema: "structure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_sections", x => x.id);
                    table.CheckConstraint("ck_class_sections_capacity", "capacity > 0");
                    table.ForeignKey(
                        name: "fk_class_sections_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalSchema: "structure",
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_sections_grade_levels_grade_level_id",
                        column: x => x.grade_level_id,
                        principalSchema: "structure",
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "id", "code", "description", "module" },
                values: new object[,]
                {
                    { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), "structure.view", null, "Structure" },
                    { new Guid("fd9f9db2-e0c7-c5e7-d196-539e1d697803"), "structure.manage", null, "Structure" }
                });

            migrationBuilder.InsertData(
                schema: "structure",
                table: "schools",
                columns: new[] { "id", "address", "currency_code", "default_locale", "email", "logo_path", "name_ar", "name_en", "phone", "timezone" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), null, "USD", "en", null, null, "SchoolOS", "SchoolOS", null, "Asia/Damascus" });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("1fcf2ced-6140-3380-daa8-c598938f90db") },
                    { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("fd9f9db2-e0c7-c5e7-d196-539e1d697803"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("fd9f9db2-e0c7-c5e7-d196-539e1d697803"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5") },
                    { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") },
                    { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("8523b3d2-c6a8-b89a-40fb-fa31b58f3989") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_academic_terms_academic_year_id_sequence",
                schema: "structure",
                table: "academic_terms",
                columns: new[] { "academic_year_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_academic_terms_one_active",
                schema: "structure",
                table: "academic_terms",
                column: "academic_year_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_academic_years_name",
                schema: "structure",
                table: "academic_years",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_academic_years_single_active",
                schema: "structure",
                table: "academic_years",
                column: "status",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_class_sections_academic_year_id_grade_level_id_name",
                schema: "structure",
                table: "class_sections",
                columns: new[] { "academic_year_id", "grade_level_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_sections_grade_level_id",
                schema: "structure",
                table: "class_sections",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_classrooms_name",
                schema: "structure",
                table: "classrooms",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_levels_sequence",
                schema: "structure",
                table: "grade_levels",
                column: "sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subjects_code",
                schema: "structure",
                table: "subjects",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_terms",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "class_sections",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "classrooms",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "schools",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "subjects",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "academic_years",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "grade_levels",
                schema: "structure");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("1fcf2ced-6140-3380-daa8-c598938f90db") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("fd9f9db2-e0c7-c5e7-d196-539e1d697803"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("fd9f9db2-e0c7-c5e7-d196-539e1d697803"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"), new Guid("8523b3d2-c6a8-b89a-40fb-fa31b58f3989") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("2bc07016-bbcf-8a39-1af1-f11226a68177"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("fd9f9db2-e0c7-c5e7-d196-539e1d697803"));
        }
    }
}
