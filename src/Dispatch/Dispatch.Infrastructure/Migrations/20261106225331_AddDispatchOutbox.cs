using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_next_attempt_at_occurred_at_id",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "occurred_at", "id" },
                filter: "processed_at IS NULL");

            // Assignments created in step 2d have no outgoing event yet.
            migrationBuilder.Sql("""
                INSERT INTO outbox_messages (id, type, payload, occurred_at, attempts, next_attempt_at)
                SELECT gen_random_uuid(), 'dispatch.courier-assigned.v1',
                    jsonb_build_object('orderId', correlation_id, 'courierId', courier_id, 'assignedAt', assigned_at),
                    assigned_at, 0, now()
                FROM assign_courier_sagas
                WHERE current_state = 'Assigned' AND courier_id IS NOT NULL AND assigned_at IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
