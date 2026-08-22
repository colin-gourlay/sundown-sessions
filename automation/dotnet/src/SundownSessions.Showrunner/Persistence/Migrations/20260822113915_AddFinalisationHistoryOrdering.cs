using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SundownSessions.Showrunner.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalisationHistoryOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BroadcastRecordings_ShowId_PlannedRecordingId",
                table: "BroadcastRecordings");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlannedRecordingId",
                table: "BroadcastRecordings",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "BroadcastRecordings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "BroadcastRecordings"
                SET "Position" = (
                    SELECT COUNT(*)
                    FROM "BroadcastRecordings" AS "prior"
                    WHERE "prior"."ShowId" = "BroadcastRecordings"."ShowId"
                      AND (
                          "prior"."BroadcastAtUtc" < "BroadcastRecordings"."BroadcastAtUtc"
                          OR ("prior"."BroadcastAtUtc" = "BroadcastRecordings"."BroadcastAtUtc" AND "prior"."Id" <= "BroadcastRecordings"."Id")
                      )
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecordings_ShowId_PlannedRecordingId",
                table: "BroadcastRecordings",
                columns: new[] { "ShowId", "PlannedRecordingId" },
                unique: true,
                filter: "\"PlannedRecordingId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecordings_ShowId_Position",
                table: "BroadcastRecordings",
                columns: new[] { "ShowId", "Position" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BroadcastRecordings_Position",
                table: "BroadcastRecordings",
                sql: "Position >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BroadcastRecordings_ShowId_PlannedRecordingId",
                table: "BroadcastRecordings");

            migrationBuilder.DropIndex(
                name: "IX_BroadcastRecordings_ShowId_Position",
                table: "BroadcastRecordings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BroadcastRecordings_Position",
                table: "BroadcastRecordings");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "BroadcastRecordings");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlannedRecordingId",
                table: "BroadcastRecordings",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecordings_ShowId_PlannedRecordingId",
                table: "BroadcastRecordings",
                columns: new[] { "ShowId", "PlannedRecordingId" },
                unique: true);
        }
    }
}
