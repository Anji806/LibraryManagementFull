using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagement.Migrations
{
    /// <inheritdoc />
    public partial class KeepBorrowHistoryOnUserDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowTransactions_Users_MemberId",
                table: "BorrowTransactions");

            migrationBuilder.AlterColumn<int>(
                name: "MemberId",
                table: "BorrowTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "MemberEmail",
                table: "BorrowTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberName",
                table: "BorrowTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowTransactions_Users_MemberId",
                table: "BorrowTransactions",
                column: "MemberId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowTransactions_Users_MemberId",
                table: "BorrowTransactions");

            migrationBuilder.DropColumn(
                name: "MemberEmail",
                table: "BorrowTransactions");

            migrationBuilder.DropColumn(
                name: "MemberName",
                table: "BorrowTransactions");

            migrationBuilder.AlterColumn<int>(
                name: "MemberId",
                table: "BorrowTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowTransactions_Users_MemberId",
                table: "BorrowTransactions",
                column: "MemberId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
