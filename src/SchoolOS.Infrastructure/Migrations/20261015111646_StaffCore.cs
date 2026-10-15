using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "staff");

            // PostgreSQL sequence for staff employee numbers (EMP-####). Sequences are not part
            // of the EF model; this statement is managed here manually.
            migrationBuilder.Sql("CREATE SEQUENCE staff.employee_number_seq AS bigint START WITH 1 INCREMENT BY 1;");


            migrationBuilder.CreateTable(
                name: "staff_members",
                schema: "staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    first_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    first_name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: false),
                    contract_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    emergency_contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_members", x => x.id);
                    table.CheckConstraint("ck_staff_members_contract_type", "contract_type IN ('FullTime', 'PartTime', 'Contract')");
                    table.CheckConstraint("ck_staff_members_status", "status IN ('Active', 'OnLeave', 'Terminated')");
                    table.ForeignKey(
                        name: "fk_staff_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "teacher_assignments",
                schema: "staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_section_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_teacher_assignments_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalSchema: "structure",
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_assignments_class_sections_class_section_id",
                        column: x => x.class_section_id,
                        principalSchema: "structure",
                        principalTable: "class_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_assignments_staff_members_staff_member_id",
                        column: x => x.staff_member_id,
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_assignments_subjects_subject_id",
                        column: x => x.subject_id,
                        principalSchema: "structure",
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "id", "code", "description", "module" },
                values: new object[,]
                {
                    { new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"), "staff.manage", null, "Staff" },
                    { new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"), "staff.view", null, "Staff" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") },
                    { new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_department",
                schema: "staff",
                table: "staff_members",
                column: "department");

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_employee_number",
                schema: "staff",
                table: "staff_members",
                column: "employee_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_status",
                schema: "staff",
                table: "staff_members",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_user_id",
                schema: "staff",
                table: "staff_members",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_class_section_id",
                schema: "staff",
                table: "teacher_assignments",
                column: "class_section_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_staff_member_id",
                schema: "staff",
                table: "teacher_assignments",
                column: "staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_subject_id",
                schema: "staff",
                table: "teacher_assignments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ux_teacher_assignments",
                schema: "staff",
                table: "teacher_assignments",
                columns: new[] { "academic_year_id", "staff_member_id", "subject_id", "class_section_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS staff.employee_number_seq;");
            migrationBuilder.DropTable(
                name: "teacher_assignments",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_members",
                schema: "staff");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("1a2eb8a4-6668-a9e6-4fb7-8ff9ed4eb689"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("71be85a8-4979-c9cb-8861-2c31230ab163"));
        }
    }
}
