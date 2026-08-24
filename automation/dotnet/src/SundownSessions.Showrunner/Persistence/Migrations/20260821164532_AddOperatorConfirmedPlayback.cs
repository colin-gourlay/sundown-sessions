using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SundownSessions.Showrunner.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorConfirmedPlayback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OperatorConfirmedAtUtc",
                table: "Reconciliations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConfirmedPlaybackItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlannedRecordingId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmedPlaybackItems", x => x.Id);
                    table.CheckConstraint("CK_ConfirmedPlaybackItems_Position", "Position >= 1");
                    table.ForeignKey(
                        name: "FK_ConfirmedPlaybackItems_PlannedRecordings_PlannedRecordingId",
                        column: x => x.PlannedRecordingId,
                        principalTable: "PlannedRecordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmedPlaybackItems_Reconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalTable: "Reconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConfirmedPlaybackItems_Recordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "Recordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmedPlaybackItems_PlannedRecordingId",
                table: "ConfirmedPlaybackItems",
                column: "PlannedRecordingId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmedPlaybackItems_ReconciliationId_PlannedRecordingId",
                table: "ConfirmedPlaybackItems",
                columns: new[] { "ReconciliationId", "PlannedRecordingId" },
                unique: true,
                filter: "\"PlannedRecordingId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmedPlaybackItems_ReconciliationId_Position",
                table: "ConfirmedPlaybackItems",
                columns: new[] { "ReconciliationId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmedPlaybackItems_RecordingId",
                table: "ConfirmedPlaybackItems",
                column: "RecordingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfirmedPlaybackItems");

            migrationBuilder.DropColumn(
                name: "OperatorConfirmedAtUtc",
                table: "Reconciliations");
        }
    }
}
