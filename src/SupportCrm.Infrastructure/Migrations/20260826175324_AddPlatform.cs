using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "TicketCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Customers",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Agents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Agents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Agents",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ContactNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LogoStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LogoContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PrimaryColorHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    SecondaryColorHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultForChannel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333301"),
                column: "DepartmentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TicketCategories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "DepartmentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TicketCategories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "DepartmentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TicketCategories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "DepartmentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TicketCategories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                column: "DepartmentId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DepartmentId",
                table: "Tickets",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_DepartmentId",
                table: "TicketCategories",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DepartmentId",
                table: "Teams",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BranchId",
                table: "Customers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_BranchId",
                table: "Agents",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_DepartmentId",
                table: "Agents",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandingSettings_BranchId",
                table: "BrandingSettings",
                column: "BranchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "BrandingSettings");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_DepartmentId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketCategories_DepartmentId",
                table: "TicketCategories");

            migrationBuilder.DropIndex(
                name: "IX_Teams_DepartmentId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Customers_BranchId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Agents_BranchId",
                table: "Agents");

            migrationBuilder.DropIndex(
                name: "IX_Agents_DepartmentId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Agents");
        }
    }
}
