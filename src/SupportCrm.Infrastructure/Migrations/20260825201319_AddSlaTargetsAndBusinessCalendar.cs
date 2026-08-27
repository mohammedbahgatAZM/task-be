using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaTargetsAndBusinessCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Tickets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "Customers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSupervisor",
                table: "Agents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AgentLanguages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLanguages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Skill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PushEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WarningThresholdPercentage = table.Column<int>(type: "integer", nullable: false),
                    DigestFrequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequiredSkill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TargetTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessHours",
                columns: table => new
                {
                    DayOfWeek = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHours", x => x.DayOfWeek);
                });

            migrationBuilder.CreateTable(
                name: "DigestLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigestLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    EscalationRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TierNumber = table.Column<int>(type: "integer", nullable: false),
                    ActionSummary = table.Column<string>(type: "text", nullable: false),
                    TriggeredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscalationRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TierNumber = table.Column<int>(type: "integer", nullable: false),
                    TriggerPercentage = table.Column<int>(type: "integer", nullable: false),
                    ReassignToAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReassignToTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    RaisePriorityTo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    NotifySupervisor = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationTiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaAlertLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaAlertLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ResponseTargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    ResolutionTargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaTargets", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BusinessHours",
                columns: new[] { "DayOfWeek", "EndTime", "IsWorkingDay", "StartTime" },
                values: new object[,]
                {
                    { "Friday", new TimeOnly(17, 0, 0), true, new TimeOnly(9, 0, 0) },
                    { "Monday", new TimeOnly(17, 0, 0), true, new TimeOnly(9, 0, 0) },
                    { "Saturday", new TimeOnly(0, 0, 0), false, new TimeOnly(0, 0, 0) },
                    { "Sunday", new TimeOnly(0, 0, 0), false, new TimeOnly(0, 0, 0) },
                    { "Thursday", new TimeOnly(17, 0, 0), true, new TimeOnly(9, 0, 0) },
                    { "Tuesday", new TimeOnly(17, 0, 0), true, new TimeOnly(9, 0, 0) },
                    { "Wednesday", new TimeOnly(17, 0, 0), true, new TimeOnly(9, 0, 0) }
                });

            migrationBuilder.InsertData(
                table: "SlaTargets",
                columns: new[] { "Id", "CategoryId", "IsActive", "Name", "Priority", "ResolutionTargetMinutes", "ResponseTargetMinutes", "Tier" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222201"), null, true, "Default — Urgent", "Urgent", 240, 30, null },
                    { new Guid("22222222-2222-2222-2222-222222222202"), null, true, "Default — High", "High", 480, 60, null },
                    { new Guid("22222222-2222-2222-2222-222222222203"), null, true, "Default — Medium", "Medium", 1440, 240, null },
                    { new Guid("22222222-2222-2222-2222-222222222204"), null, true, "Default — Low", "Low", 4320, 480, null }
                });

            migrationBuilder.InsertData(
                table: "Teams",
                columns: new[] { "Id", "Name" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333301"), "General Queue" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentLanguages_AgentId_Language",
                table: "AgentLanguages",
                columns: new[] { "AgentId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSkills_AgentId_Skill",
                table: "AgentSkills",
                columns: new[] { "AgentId", "Skill" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertPreferences_AgentId",
                table: "AlertPreferences",
                column: "AgentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRules_SortOrder",
                table: "AssignmentRules",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_DigestLog_AgentId",
                table: "DigestLog",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationLog_TicketId",
                table: "EscalationLog",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationLog_TicketId_EscalationRuleId_TierNumber",
                table: "EscalationLog",
                columns: new[] { "TicketId", "EscalationRuleId", "TierNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_SortOrder",
                table: "EscalationRules",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationTiers_EscalationRuleId_TierNumber",
                table: "EscalationTiers",
                columns: new[] { "EscalationRuleId", "TierNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date",
                table: "Holidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaAlertLog_TicketId_Kind",
                table: "SlaAlertLog",
                columns: new[] { "TicketId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaTargets_Priority_CategoryId_Tier",
                table: "SlaTargets",
                columns: new[] { "Priority", "CategoryId", "Tier" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentLanguages");

            migrationBuilder.DropTable(
                name: "AgentSkills");

            migrationBuilder.DropTable(
                name: "AlertPreferences");

            migrationBuilder.DropTable(
                name: "AssignmentRules");

            migrationBuilder.DropTable(
                name: "BusinessHours");

            migrationBuilder.DropTable(
                name: "DigestLog");

            migrationBuilder.DropTable(
                name: "EscalationLog");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropTable(
                name: "EscalationTiers");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "SlaAlertLog");

            migrationBuilder.DropTable(
                name: "SlaTargets");

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333301"));

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsSupervisor",
                table: "Agents");
        }
    }
}
