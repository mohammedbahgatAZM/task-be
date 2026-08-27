using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAndAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ActionSummary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsSystemDefined = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MfaSecret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Module" },
                values: new object[,]
                {
                    { new Guid("66666666-6666-6666-6666-000000000000"), "View", "Tickets" },
                    { new Guid("66666666-6666-6666-6666-000000000001"), "Create", "Tickets" },
                    { new Guid("66666666-6666-6666-6666-000000000002"), "Edit", "Tickets" },
                    { new Guid("66666666-6666-6666-6666-000000000003"), "Delete", "Tickets" },
                    { new Guid("66666666-6666-6666-6666-000000000004"), "Export", "Tickets" },
                    { new Guid("66666666-6666-6666-6666-000000000005"), "View", "Customers" },
                    { new Guid("66666666-6666-6666-6666-000000000006"), "Create", "Customers" },
                    { new Guid("66666666-6666-6666-6666-000000000007"), "Edit", "Customers" },
                    { new Guid("66666666-6666-6666-6666-000000000008"), "Delete", "Customers" },
                    { new Guid("66666666-6666-6666-6666-000000000009"), "Export", "Customers" },
                    { new Guid("66666666-6666-6666-6666-000000000010"), "View", "KnowledgeBase" },
                    { new Guid("66666666-6666-6666-6666-000000000011"), "Create", "KnowledgeBase" },
                    { new Guid("66666666-6666-6666-6666-000000000012"), "Edit", "KnowledgeBase" },
                    { new Guid("66666666-6666-6666-6666-000000000013"), "Delete", "KnowledgeBase" },
                    { new Guid("66666666-6666-6666-6666-000000000014"), "Export", "KnowledgeBase" },
                    { new Guid("66666666-6666-6666-6666-000000000015"), "View", "Sla" },
                    { new Guid("66666666-6666-6666-6666-000000000016"), "Create", "Sla" },
                    { new Guid("66666666-6666-6666-6666-000000000017"), "Edit", "Sla" },
                    { new Guid("66666666-6666-6666-6666-000000000018"), "Delete", "Sla" },
                    { new Guid("66666666-6666-6666-6666-000000000019"), "Export", "Sla" },
                    { new Guid("66666666-6666-6666-6666-000000000020"), "View", "Ai" },
                    { new Guid("66666666-6666-6666-6666-000000000021"), "Create", "Ai" },
                    { new Guid("66666666-6666-6666-6666-000000000022"), "Edit", "Ai" },
                    { new Guid("66666666-6666-6666-6666-000000000023"), "Delete", "Ai" },
                    { new Guid("66666666-6666-6666-6666-000000000024"), "Export", "Ai" },
                    { new Guid("66666666-6666-6666-6666-000000000025"), "View", "CustomerPortal" },
                    { new Guid("66666666-6666-6666-6666-000000000026"), "Create", "CustomerPortal" },
                    { new Guid("66666666-6666-6666-6666-000000000027"), "Edit", "CustomerPortal" },
                    { new Guid("66666666-6666-6666-6666-000000000028"), "Delete", "CustomerPortal" },
                    { new Guid("66666666-6666-6666-6666-000000000029"), "Export", "CustomerPortal" },
                    { new Guid("66666666-6666-6666-6666-000000000030"), "View", "Reports" },
                    { new Guid("66666666-6666-6666-6666-000000000031"), "Create", "Reports" },
                    { new Guid("66666666-6666-6666-6666-000000000032"), "Edit", "Reports" },
                    { new Guid("66666666-6666-6666-6666-000000000033"), "Delete", "Reports" },
                    { new Guid("66666666-6666-6666-6666-000000000034"), "Export", "Reports" },
                    { new Guid("66666666-6666-6666-6666-000000000035"), "View", "Administration" },
                    { new Guid("66666666-6666-6666-6666-000000000036"), "Create", "Administration" },
                    { new Guid("66666666-6666-6666-6666-000000000037"), "Edit", "Administration" },
                    { new Guid("66666666-6666-6666-6666-000000000038"), "Delete", "Administration" },
                    { new Guid("66666666-6666-6666-6666-000000000039"), "Export", "Administration" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("77777777-7777-7777-7777-000000000000"), new Guid("66666666-6666-6666-6666-000000000000"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000001"), new Guid("66666666-6666-6666-6666-000000000001"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000002"), new Guid("66666666-6666-6666-6666-000000000002"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000003"), new Guid("66666666-6666-6666-6666-000000000003"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000004"), new Guid("66666666-6666-6666-6666-000000000004"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000005"), new Guid("66666666-6666-6666-6666-000000000005"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000006"), new Guid("66666666-6666-6666-6666-000000000006"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000007"), new Guid("66666666-6666-6666-6666-000000000007"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000008"), new Guid("66666666-6666-6666-6666-000000000008"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000009"), new Guid("66666666-6666-6666-6666-000000000009"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000010"), new Guid("66666666-6666-6666-6666-000000000010"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000011"), new Guid("66666666-6666-6666-6666-000000000011"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000012"), new Guid("66666666-6666-6666-6666-000000000012"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000013"), new Guid("66666666-6666-6666-6666-000000000013"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000014"), new Guid("66666666-6666-6666-6666-000000000014"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000015"), new Guid("66666666-6666-6666-6666-000000000015"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000016"), new Guid("66666666-6666-6666-6666-000000000016"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000017"), new Guid("66666666-6666-6666-6666-000000000017"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000018"), new Guid("66666666-6666-6666-6666-000000000018"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000019"), new Guid("66666666-6666-6666-6666-000000000019"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000020"), new Guid("66666666-6666-6666-6666-000000000020"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000021"), new Guid("66666666-6666-6666-6666-000000000021"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000022"), new Guid("66666666-6666-6666-6666-000000000022"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000023"), new Guid("66666666-6666-6666-6666-000000000023"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000024"), new Guid("66666666-6666-6666-6666-000000000024"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000025"), new Guid("66666666-6666-6666-6666-000000000025"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000026"), new Guid("66666666-6666-6666-6666-000000000026"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000027"), new Guid("66666666-6666-6666-6666-000000000027"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000028"), new Guid("66666666-6666-6666-6666-000000000028"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000029"), new Guid("66666666-6666-6666-6666-000000000029"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000030"), new Guid("66666666-6666-6666-6666-000000000030"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000031"), new Guid("66666666-6666-6666-6666-000000000031"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000032"), new Guid("66666666-6666-6666-6666-000000000032"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000033"), new Guid("66666666-6666-6666-6666-000000000033"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000034"), new Guid("66666666-6666-6666-6666-000000000034"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000035"), new Guid("66666666-6666-6666-6666-000000000035"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000036"), new Guid("66666666-6666-6666-6666-000000000036"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000037"), new Guid("66666666-6666-6666-6666-000000000037"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000038"), new Guid("66666666-6666-6666-6666-000000000038"), new Guid("55555555-5555-5555-5555-555555555505") },
                    { new Guid("77777777-7777-7777-7777-000000000039"), new Guid("66666666-6666-6666-6666-000000000039"), new Guid("55555555-5555-5555-5555-555555555505") }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "IsSystemDefined", "Name" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555502"), true, "Agent" },
                    { new Guid("55555555-5555-5555-5555-555555555503"), true, "Team Lead" },
                    { new Guid("55555555-5555-5555-5555-555555555504"), true, "Manager" },
                    { new Guid("55555555-5555-5555-5555-555555555505"), true, "Admin" }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Id", "RoleId", "UserId" },
                values: new object[] { new Guid("55555555-5555-5555-5555-555555555601"), new Guid("55555555-5555-5555-5555-555555555505"), new Guid("55555555-5555-5555-5555-555555555501") });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAtUtc", "Email", "FailedLoginAttempts", "IsActive", "LockedUntilUtc", "MfaEnabled", "MfaSecret", "PasswordChangedAtUtc", "PasswordHash" },
                values: new object[] { new Guid("55555555-5555-5555-5555-555555555501"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "admin@supportcrm.local", 0, true, null, false, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "AQAAAAIAAYagAAAAENB/BIDiPZplJMqE54eutY2QGsuAYNAxN4m/ltMy75o9lbelpJ2Op7u7DEm+O9vRXA==" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_OccurredAtUtc",
                table: "AuditLogEntries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_UserId",
                table: "AuditLogEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Module_Action",
                table: "Permissions",
                columns: new[] { "Module", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
