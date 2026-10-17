using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MagicLinkTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "magic_link_tokens",
                schema: "users",
                columns: table => new
                {
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    return_to = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_link_tokens", x => x.token_hash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_email_created_at_utc",
                schema: "users",
                table: "magic_link_tokens",
                columns: new[] { "email", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_expires_at_utc",
                schema: "users",
                table: "magic_link_tokens",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_used_at_utc",
                schema: "users",
                table: "magic_link_tokens",
                column: "used_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "magic_link_tokens",
                schema: "users");
        }
    }
}
