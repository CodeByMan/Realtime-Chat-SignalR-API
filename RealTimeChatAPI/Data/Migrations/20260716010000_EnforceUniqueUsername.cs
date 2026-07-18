using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealTimeChatAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT LOWER([Username])
                    FROM [Users]
                    GROUP BY LOWER([Username])
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Cannot enforce unique usernames because duplicate normalized usernames exist.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [Users]
                    WHERE DATALENGTH([Username]) > 40
                )
                    THROW 51001, 'Cannot limit usernames to 20 characters because an existing username exceeds the limit.', 1;

                UPDATE [Users]
                SET [Username] = LOWER([Username]);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
