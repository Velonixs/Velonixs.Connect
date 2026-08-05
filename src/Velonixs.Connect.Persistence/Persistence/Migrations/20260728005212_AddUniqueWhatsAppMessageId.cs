using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueWhatsAppMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_MessageLog_WhatsAppMessageId'
                      AND [object_id] = OBJECT_ID(N'[MessageLog]')
                )
                BEGIN
                    DROP INDEX [IX_MessageLog_WhatsAppMessageId] ON [MessageLog];
                END
                """);

            // Preserve the earliest historic provider ID and clear only later
            // duplicates before enforcing exactly-once inbound processing.
            migrationBuilder.Sql(@"
                ;WITH DuplicateMessageIds AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [WhatsAppMessageId]
                               ORDER BY [CreatedAt], [Id]
                           ) AS [DuplicateNumber]
                    FROM [MessageLog]
                    WHERE [WhatsAppMessageId] IS NOT NULL
                )
                UPDATE [MessageLog]
                SET [WhatsAppMessageId] = NULL
                FROM [MessageLog]
                INNER JOIN DuplicateMessageIds
                    ON DuplicateMessageIds.[Id] = [MessageLog].[Id]
                WHERE DuplicateMessageIds.[DuplicateNumber] > 1;");

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_MessageLog_WhatsAppMessageId'
                      AND [object_id] = OBJECT_ID(N'[MessageLog]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_MessageLog_WhatsAppMessageId]
                    ON [MessageLog] ([WhatsAppMessageId])
                    WHERE [WhatsAppMessageId] IS NOT NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageLog_WhatsAppMessageId",
                table: "MessageLog");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLog_WhatsAppMessageId",
                table: "MessageLog",
                column: "WhatsAppMessageId");
        }
    }
}
