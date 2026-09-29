using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlagForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FlagId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResourceKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Before = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    After = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlagUsageHourly",
                columns: table => new
                {
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    HourStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlagUsageHourly", x => new { x.EnvironmentId, x.FlagId, x.VariationId, x.HourStart });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Environments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    IsProtected = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ConfigVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Environments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Variations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Salt = table.Column<string>(type: "char(16)", unicode: false, fixedLength: true, maxLength: 16, nullable: false),
                    IsPermanent = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flags_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Flags_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SdkKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    KeyHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SdkKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SdkKeys_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SdkKeys_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FlagEnvironmentConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    OffVariationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Targets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rules = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fallthrough = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlagEnvironmentConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlagEnvironmentConfigs_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FlagEnvironmentConfigs_Flags_FlagId",
                        column: x => x.FlagId,
                        principalTable: "Flags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlagEnvironmentConfigs_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScheduledChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecuteAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ClaimedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReleasePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledChanges_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduledChanges_Flags_FlagId",
                        column: x => x.FlagId,
                        principalTable: "Flags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledChanges_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_FlagId_OccurredAt",
                table: "AuditEntries",
                columns: new[] { "FlagId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAt",
                table: "AuditEntries",
                column: "OccurredAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ProjectId_OccurredAt",
                table: "AuditEntries",
                columns: new[] { "ProjectId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Environments_ProjectId_Key",
                table: "Environments",
                columns: new[] { "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlagEnvironmentConfigs_EnvironmentId",
                table: "FlagEnvironmentConfigs",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FlagEnvironmentConfigs_FlagId_EnvironmentId",
                table: "FlagEnvironmentConfigs",
                columns: new[] { "FlagId", "EnvironmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlagEnvironmentConfigs_UpdatedByUserId",
                table: "FlagEnvironmentConfigs",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FlagUsageHourly_FlagId_HourStart",
                table: "FlagUsageHourly",
                columns: new[] { "FlagId", "HourStart" });

            migrationBuilder.CreateIndex(
                name: "IX_FlagUsageHourly_HourStart",
                table: "FlagUsageHourly",
                column: "HourStart");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_CreatedByUserId",
                table: "Flags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_ProjectId_Key",
                table: "Flags",
                columns: new[] { "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                table: "Projects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledChanges_CreatedByUserId",
                table: "ScheduledChanges",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledChanges_EnvironmentId",
                table: "ScheduledChanges",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledChanges_FlagId_EnvironmentId",
                table: "ScheduledChanges",
                columns: new[] { "FlagId", "EnvironmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledChanges_Status_ExecuteAt",
                table: "ScheduledChanges",
                columns: new[] { "Status", "ExecuteAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SdkKeys_CreatedByUserId",
                table: "SdkKeys",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SdkKeys_EnvironmentId",
                table: "SdkKeys",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SdkKeys_KeyHash",
                table: "SdkKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "FlagEnvironmentConfigs");

            migrationBuilder.DropTable(
                name: "FlagUsageHourly");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ScheduledChanges");

            migrationBuilder.DropTable(
                name: "SdkKeys");

            migrationBuilder.DropTable(
                name: "Flags");

            migrationBuilder.DropTable(
                name: "Environments");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
