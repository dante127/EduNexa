using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    preferred_language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "en"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_preferred_language", "preferred_language IN ('en', 'ar')");
                    table.CheckConstraint("ck_users_status", "status IN ('Invited', 'Active', 'Deactivated')");
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_refresh_tokens_replaced_by_id",
                        column: x => x.replaced_by_id,
                        principalSchema: "identity",
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                schema: "identity",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "id", "code", "description", "module" },
                values: new object[,]
                {
                    { new Guid("016c13cf-0261-f7db-fc71-b6fd9b6ff587"), "grades.approve", null, "Academics" },
                    { new Guid("1921aa39-2e33-657a-9278-bfabde3e3df6"), "announcements.manage", null, "Communication" },
                    { new Guid("1edbcc39-4135-d300-c46b-3b9151d3add2"), "attendance.take", null, "Attendance" },
                    { new Guid("37be1b56-e8ca-c12c-198a-25266e2ed579"), "grades.enter", null, "Academics" },
                    { new Guid("6a2cd00a-719e-78f8-7c47-7a4bd362baf8"), "students.manage", null, "Students" },
                    { new Guid("7bb04342-2071-5350-a0aa-70def9cfa151"), "grades.publish", null, "Academics" },
                    { new Guid("86a31d72-1a02-f410-550f-ace9095de30b"), "students.view", null, "Students" },
                    { new Guid("8929c905-c3a8-77e2-ffe5-3781d5a3115b"), "attendance.correct", null, "Attendance" },
                    { new Guid("9bd53c1d-0058-e968-d5f0-d069e8bfe9fd"), "finance.invoices.manage", null, "Finance" },
                    { new Guid("9f05554b-81ae-2c98-93d2-683782d48873"), "reports.view", null, "Reports" },
                    { new Guid("a9b701e3-66fe-b890-f40f-7f124ffa4a62"), "admin.users.manage", null, "Administration" },
                    { new Guid("b988373f-1e1a-f2f7-46a0-500d47a29468"), "finance.refunds.approve", null, "Finance" },
                    { new Guid("bd5f1206-25f8-1437-9f84-7a468bad35f0"), "finance.payments.post", null, "Finance" },
                    { new Guid("ec69b5d1-26f5-fb11-cce0-423e74d34ed0"), "homework.manage", null, "Learning" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "roles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[,]
                {
                    { new Guid("1fcf2ced-6140-3380-daa8-c598938f90db"), "1fcf2ced-6140-3380-daa8-c598938f90db", "Accountant", "ACCOUNTANT" },
                    { new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b"), "3249fe97-e57b-5a9a-7bc6-d2023e8b864b", "SuperAdmin", "SUPERADMIN" },
                    { new Guid("4a74b031-8f5a-4030-1226-f2faeae43969"), "4a74b031-8f5a-4030-1226-f2faeae43969", "SchoolAdmin", "SCHOOLADMIN" },
                    { new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5"), "66bcb3ad-a449-ded8-d542-3596fc6264b5", "Teacher", "TEACHER" },
                    { new Guid("697ed8f0-4130-4606-d25f-53efa816fe4e"), "697ed8f0-4130-4606-d25f-53efa816fe4e", "Student", "STUDENT" },
                    { new Guid("765e0a71-6e8c-d03c-5c1e-b83395836ba9"), "765e0a71-6e8c-d03c-5c1e-b83395836ba9", "Parent", "PARENT" },
                    { new Guid("849289eb-e846-1afd-e808-6d17a8249154"), "849289eb-e846-1afd-e808-6d17a8249154", "Principal", "PRINCIPAL" },
                    { new Guid("8523b3d2-c6a8-b89a-40fb-fa31b58f3989"), "8523b3d2-c6a8-b89a-40fb-fa31b58f3989", "Receptionist", "RECEPTIONIST" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("9bd53c1d-0058-e968-d5f0-d069e8bfe9fd"), new Guid("1fcf2ced-6140-3380-daa8-c598938f90db") },
                    { new Guid("9f05554b-81ae-2c98-93d2-683782d48873"), new Guid("1fcf2ced-6140-3380-daa8-c598938f90db") },
                    { new Guid("bd5f1206-25f8-1437-9f84-7a468bad35f0"), new Guid("1fcf2ced-6140-3380-daa8-c598938f90db") },
                    { new Guid("016c13cf-0261-f7db-fc71-b6fd9b6ff587"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("1921aa39-2e33-657a-9278-bfabde3e3df6"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("1edbcc39-4135-d300-c46b-3b9151d3add2"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("37be1b56-e8ca-c12c-198a-25266e2ed579"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("6a2cd00a-719e-78f8-7c47-7a4bd362baf8"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("7bb04342-2071-5350-a0aa-70def9cfa151"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("86a31d72-1a02-f410-550f-ace9095de30b"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("8929c905-c3a8-77e2-ffe5-3781d5a3115b"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("9bd53c1d-0058-e968-d5f0-d069e8bfe9fd"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("9f05554b-81ae-2c98-93d2-683782d48873"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("a9b701e3-66fe-b890-f40f-7f124ffa4a62"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("b988373f-1e1a-f2f7-46a0-500d47a29468"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("bd5f1206-25f8-1437-9f84-7a468bad35f0"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("ec69b5d1-26f5-fb11-cce0-423e74d34ed0"), new Guid("3249fe97-e57b-5a9a-7bc6-d2023e8b864b") },
                    { new Guid("016c13cf-0261-f7db-fc71-b6fd9b6ff587"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("1921aa39-2e33-657a-9278-bfabde3e3df6"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("1edbcc39-4135-d300-c46b-3b9151d3add2"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("37be1b56-e8ca-c12c-198a-25266e2ed579"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("6a2cd00a-719e-78f8-7c47-7a4bd362baf8"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("7bb04342-2071-5350-a0aa-70def9cfa151"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("86a31d72-1a02-f410-550f-ace9095de30b"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("8929c905-c3a8-77e2-ffe5-3781d5a3115b"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("9bd53c1d-0058-e968-d5f0-d069e8bfe9fd"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("9f05554b-81ae-2c98-93d2-683782d48873"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("a9b701e3-66fe-b890-f40f-7f124ffa4a62"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("b988373f-1e1a-f2f7-46a0-500d47a29468"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("bd5f1206-25f8-1437-9f84-7a468bad35f0"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("ec69b5d1-26f5-fb11-cce0-423e74d34ed0"), new Guid("4a74b031-8f5a-4030-1226-f2faeae43969") },
                    { new Guid("1edbcc39-4135-d300-c46b-3b9151d3add2"), new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5") },
                    { new Guid("37be1b56-e8ca-c12c-198a-25266e2ed579"), new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5") },
                    { new Guid("86a31d72-1a02-f410-550f-ace9095de30b"), new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5") },
                    { new Guid("8929c905-c3a8-77e2-ffe5-3781d5a3115b"), new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5") },
                    { new Guid("ec69b5d1-26f5-fb11-cce0-423e74d34ed0"), new Guid("66bcb3ad-a449-ded8-d542-3596fc6264b5") },
                    { new Guid("016c13cf-0261-f7db-fc71-b6fd9b6ff587"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") },
                    { new Guid("7bb04342-2071-5350-a0aa-70def9cfa151"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") },
                    { new Guid("86a31d72-1a02-f410-550f-ace9095de30b"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") },
                    { new Guid("9f05554b-81ae-2c98-93d2-683782d48873"), new Guid("849289eb-e846-1afd-e808-6d17a8249154") },
                    { new Guid("6a2cd00a-719e-78f8-7c47-7a4bd362baf8"), new Guid("8523b3d2-c6a8-b89a-40fb-fa31b58f3989") },
                    { new Guid("86a31d72-1a02-f410-550f-ace9095de30b"), new Guid("8523b3d2-c6a8-b89a-40fb-fa31b58f3989") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_permissions_code",
                schema: "identity",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_replaced_by_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "replaced_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "identity",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_active",
                schema: "identity",
                table: "refresh_tokens",
                column: "user_id",
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                schema: "identity",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                schema: "identity",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                schema: "identity",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                schema: "identity",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                schema: "identity",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "users",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_claims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_claims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_logins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
