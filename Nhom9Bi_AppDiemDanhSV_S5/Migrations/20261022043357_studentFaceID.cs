using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nhom9Bi_AppDiemDanhSV_S5.Migrations
{
    /// <inheritdoc />
    public partial class studentFaceID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaceEmbedding",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FaceRegisteredAt",
                table: "Students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFaceRegistered",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaceEmbedding",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FaceRegisteredAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsFaceRegistered",
                table: "Students");
        }
    }
}
