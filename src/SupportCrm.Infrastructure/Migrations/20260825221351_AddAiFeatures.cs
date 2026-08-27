using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "ChatSessions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SolutionSuggestionFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlaggedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FlaggedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionSuggestionFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketAiSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SummaryText = table.Column<string>(type: "text", nullable: false),
                    SourceMessageCount = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAiSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketCategorizationSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuggestedPriority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConfidencePercentage = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategorizationSuggestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolutionSuggestionFeedback_ContentType_ContentId",
                table: "SolutionSuggestionFeedback",
                columns: new[] { "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiSummaries_TicketId",
                table: "TicketAiSummaries",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategorizationSuggestions_TicketId",
                table: "TicketCategorizationSuggestions",
                column: "TicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolutionSuggestionFeedback");

            migrationBuilder.DropTable(
                name: "TicketAiSummaries");

            migrationBuilder.DropTable(
                name: "TicketCategorizationSuggestions");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "ChatSessions");
        }
    }
}
