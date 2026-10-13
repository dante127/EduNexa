using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchoolOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StudentsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "students");

            migrationBuilder.CreateTable(
                name: "admission_applications",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_first_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    applicant_last_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    applicant_first_name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    applicant_last_name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nationality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desired_grade_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    guardian_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    guardian_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    converted_student_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_applications", x => x.id);
                    table.CheckConstraint("ck_admission_applications_status", "status IN ('Submitted', 'UnderReview', 'Approved', 'Rejected', 'Enrolled')");
                    table.ForeignKey(
                        name: "fk_admission_applications_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalSchema: "structure",
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_applications_grade_levels_desired_grade_level_id",
                        column: x => x.desired_grade_level_id,
                        principalSchema: "structure",
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guardians",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name_en = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    full_name_ar = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    alt_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occupation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guardians", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_number_counters",
                schema: "students",
                columns: table => new
                {
                    year = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_number_counters", x => x.year);
                });

            migrationBuilder.CreateTable(
                name: "students",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    first_name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nationality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    photo_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_students", x => x.id);
                    table.CheckConstraint("ck_students_gender", "gender IN ('Male', 'Female')");
                    table.CheckConstraint("ck_students_status", "status IN ('Applicant', 'Active', 'Transferred', 'Withdrawn', 'Graduated')");
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.CheckConstraint("ck_enrollments_status", "status IN ('Enrolled', 'Completed', 'Transferred', 'Withdrawn', 'Graduated')");
                    table.ForeignKey(
                        name: "fk_enrollments_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalSchema: "structure",
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_class_sections_class_section_id",
                        column: x => x.class_section_id,
                        principalSchema: "structure",
                        principalTable: "class_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_grade_levels_grade_level_id",
                        column: x => x.grade_level_id,
                        principalSchema: "structure",
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "students",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_guardians",
                schema: "students",
                columns: table => new
                {
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_emergency_contact = table.Column<bool>(type: "boolean", nullable: false),
                    is_authorized_pickup = table.Column<bool>(type: "boolean", nullable: false),
                    custody_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_guardians", x => new { x.student_id, x.guardian_id });
                    table.CheckConstraint("ck_student_guardians_relationship", "relationship IN ('Father', 'Mother', 'Guardian', 'Other')");
                    table.ForeignKey(
                        name: "fk_student_guardians_guardians_guardian_id",
                        column: x => x.guardian_id,
                        principalSchema: "students",
                        principalTable: "guardians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_guardians_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "students",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_academic_year_id",
                schema: "students",
                table: "admission_applications",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_desired_grade_level_id",
                schema: "students",
                table: "admission_applications",
                column: "desired_grade_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_applications_status",
                schema: "students",
                table: "admission_applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_academic_year_id",
                schema: "students",
                table: "enrollments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_class_section_id",
                schema: "students",
                table: "enrollments",
                column: "class_section_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_grade_level_id",
                schema: "students",
                table: "enrollments",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_student_id",
                schema: "students",
                table: "enrollments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ux_enrollments_open",
                schema: "students",
                table: "enrollments",
                columns: new[] { "student_id", "academic_year_id" },
                unique: true,
                filter: "status = 'Enrolled'");

            migrationBuilder.CreateIndex(
                name: "ix_guardians_full_name_en",
                schema: "students",
                table: "guardians",
                column: "full_name_en");

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_guardian_id",
                schema: "students",
                table: "student_guardians",
                column: "guardian_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_last_name_en_first_name_en",
                schema: "students",
                table: "students",
                columns: new[] { "last_name_en", "first_name_en" });

            migrationBuilder.CreateIndex(
                name: "ix_students_status",
                schema: "students",
                table: "students",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_students_student_number",
                schema: "students",
                table: "students",
                column: "student_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admission_applications",
                schema: "students");

            migrationBuilder.DropTable(
                name: "enrollments",
                schema: "students");

            migrationBuilder.DropTable(
                name: "student_guardians",
                schema: "students");

            migrationBuilder.DropTable(
                name: "student_number_counters",
                schema: "students");

            migrationBuilder.DropTable(
                name: "guardians",
                schema: "students");

            migrationBuilder.DropTable(
                name: "students",
                schema: "students");
        }
    }
}
