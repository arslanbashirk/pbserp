using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PBS.ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRefreshTokenTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_USER_REFRESH_TOKEN",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    JwtId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReasonRevoked = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_USER_REFRESH_TOKEN", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_USER_REFRESH_TOKEN_SYS_USER_UserId",
                        column: x => x.UserId,
                        principalTable: "SYS_USER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USER_REFRESH_TOKEN_ExpiresAtUtc",
                table: "SYS_USER_REFRESH_TOKEN",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USER_REFRESH_TOKEN_RevokedAtUtc",
                table: "SYS_USER_REFRESH_TOKEN",
                column: "RevokedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USER_REFRESH_TOKEN_TokenHash",
                table: "SYS_USER_REFRESH_TOKEN",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USER_REFRESH_TOKEN_UserId",
                table: "SYS_USER_REFRESH_TOKEN",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SYS_USER_REFRESH_TOKEN");
        }
    }
}
