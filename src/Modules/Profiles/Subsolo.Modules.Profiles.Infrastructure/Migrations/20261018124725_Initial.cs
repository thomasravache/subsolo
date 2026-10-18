using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Profiles.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "profiles");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "profile_requests",
                schema: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    contact_or_proof = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                schema: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    city = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    styles = table.Column<string[]>(type: "text[]", nullable: false),
                    links = table.Column<string>(type: "jsonb", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_message_consumers",
                schema: "profiles",
                columns: table => new
                {
                    inbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_message_consumers", x => new { x.inbox_message_id, x.name });
                    table.ForeignKey(
                        name: "FK_inbox_message_consumers_inbox_messages_inbox_message_id",
                        column: x => x.inbox_message_id,
                        principalSchema: "profiles",
                        principalTable: "inbox_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_message_consumers",
                schema: "profiles",
                columns: table => new
                {
                    outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message_consumers", x => new { x.outbox_message_id, x.name });
                    table.ForeignKey(
                        name: "FK_outbox_message_consumers_outbox_messages_outbox_message_id",
                        column: x => x.outbox_message_id,
                        principalSchema: "profiles",
                        principalTable: "outbox_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_pending",
                schema: "profiles",
                table: "inbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "profiles",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_profile_requests_decided_at_utc",
                schema: "profiles",
                table: "profile_requests",
                column: "decided_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_profile_requests_requester_user_id",
                schema: "profiles",
                table: "profile_requests",
                column: "requester_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_requests_status_created_at_utc",
                schema: "profiles",
                table: "profile_requests",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_profiles_owner_user_id",
                schema: "profiles",
                table: "profiles",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_profiles_type_slug",
                schema: "profiles",
                table: "profiles",
                columns: new[] { "type", "slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_message_consumers",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "outbox_message_consumers",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "profile_requests",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "profiles",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "profiles");
        }
    }
}
