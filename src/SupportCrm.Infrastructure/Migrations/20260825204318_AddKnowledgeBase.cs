using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enables trigram similarity matching (EF.Functions.TrigramsSimilarity) used by
            // Knowledge Base Story 28's best-effort fuzzy search, if the installed Npgsql EF
            // provider supports it — see KbSearchService's Edge Cases notes.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.AddColumn<bool>(
                name: "IsKnowledgeBaseEditor",
                table: "Agents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ArticleAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KbCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    TitleEn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TitleAr = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BodyEn = table.Column<string>(type: "text", nullable: true),
                    BodyAr = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastUpdatedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    HelpfulCount = table.Column<int>(type: "integer", nullable: false),
                    NotHelpfulCount = table.Column<int>(type: "integer", nullable: false),
                    HasBeenPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    TitleEnSnapshot = table.Column<string>(type: "text", nullable: true),
                    TitleArSnapshot = table.Column<string>(type: "text", nullable: true),
                    BodyEnSnapshot = table.Column<string>(type: "text", nullable: true),
                    BodyArSnapshot = table.Column<string>(type: "text", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KbCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionEn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    QuestionAr = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AnswerEn = table.Column<string>(type: "text", nullable: true),
                    AnswerAr = table.Column<string>(type: "text", nullable: true),
                    HelpfulCount = table.Column<int>(type: "integer", nullable: false),
                    NotHelpfulCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuideAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TitleAr = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BodyEn = table.Column<string>(type: "text", nullable: true),
                    BodyAr = table.Column<string>(type: "text", nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastUpdatedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFlaggedOutdated = table.Column<bool>(type: "boolean", nullable: false),
                    FlaggedReason = table.Column<string>(type: "text", nullable: true),
                    FlaggedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HasBeenPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuideTicketCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideTicketCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KbCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NameEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    SearchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAttachments_ArticleId",
                table: "ArticleAttachments",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_KbCategoryId",
                table: "Articles",
                column: "KbCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Status",
                table: "Articles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersions_ContentType_ContentId_VersionNumber",
                table: "ContentVersions",
                columns: new[] { "ContentType", "ContentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faqs_KbCategoryId",
                table: "Faqs",
                column: "KbCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GuideAttachments_GuideId",
                table: "GuideAttachments",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_Guides_Status",
                table: "Guides",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GuideTicketCategories_GuideId_TicketCategoryId",
                table: "GuideTicketCategories",
                columns: new[] { "GuideId", "TicketCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchLogs_ResultCount",
                table: "SearchLogs",
                column: "ResultCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleAttachments");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "ContentVersions");

            migrationBuilder.DropTable(
                name: "Faqs");

            migrationBuilder.DropTable(
                name: "GuideAttachments");

            migrationBuilder.DropTable(
                name: "Guides");

            migrationBuilder.DropTable(
                name: "GuideTicketCategories");

            migrationBuilder.DropTable(
                name: "KbCategories");

            migrationBuilder.DropTable(
                name: "SearchLogs");

            migrationBuilder.DropColumn(
                name: "IsKnowledgeBaseEditor",
                table: "Agents");
        }
    }
}
