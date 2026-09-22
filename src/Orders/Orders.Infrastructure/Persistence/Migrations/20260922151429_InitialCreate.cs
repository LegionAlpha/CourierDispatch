using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    courier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    price_minor = table.Column<long>(type: "bigint", nullable: false),
                    eta_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    from_latitude = table.Column<double>(type: "double precision", nullable: false),
                    from_longitude = table.Column<double>(type: "double precision", nullable: false),
                    to_latitude = table.Column<double>(type: "double precision", nullable: false),
                    to_longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                table: "orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
