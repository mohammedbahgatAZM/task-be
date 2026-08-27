using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaqPortalImpressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FaqId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftSessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LedToTicketSubmission = table.Column<bool>(type: "boolean", nullable: false),
                    ShownAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqPortalImpressions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketFeedback", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaqPortalImpressions_DraftSessionId",
                table: "FaqPortalImpressions",
                column: "DraftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FaqPortalImpressions_FaqId",
                table: "FaqPortalImpressions",
                column: "FaqId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketFeedback_TicketId",
                table: "TicketFeedback",
                column: "TicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaqPortalImpressions");

            migrationBuilder.DropTable(
                name: "TicketFeedback");
        }
    }
}
