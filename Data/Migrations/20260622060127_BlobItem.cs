using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Calendare.Data.Migrations
{
    /// <inheritdoc />
    public partial class BlobItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "ak_collection_object_uri",
                table: "collection_object");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_collection_uri",
                table: "collection");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "usr_credential",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer",
                table: "usr_credential",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "segment",
                table: "collection_object",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "segment",
                table: "collection",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "object_blob",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    collection_object_id = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    content_length = table.Column<long>(type: "bigint", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    language_code = table.Column<string>(type: "text", nullable: true),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_access = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_blob", x => x.id);
                    table.ForeignKey(
                        name: "fk_object_blob_collection_object_collection_object_id",
                        column: x => x.collection_object_id,
                        principalTable: "collection_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collection_object_uri",
                table: "collection_object",
                column: "uri",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_collection_uri",
                table: "collection",
                column: "uri",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_object_blob_collection_object_id",
                table: "object_blob",
                column: "collection_object_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "object_blob");

            migrationBuilder.DropIndex(
                name: "ix_collection_object_uri",
                table: "collection_object");

            migrationBuilder.DropIndex(
                name: "ix_collection_uri",
                table: "collection");

            migrationBuilder.DropColumn(
                name: "description",
                table: "usr_credential");

            migrationBuilder.DropColumn(
                name: "issuer",
                table: "usr_credential");

            migrationBuilder.DropColumn(
                name: "segment",
                table: "collection_object");

            migrationBuilder.DropColumn(
                name: "segment",
                table: "collection");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_collection_object_uri",
                table: "collection_object",
                column: "uri");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_collection_uri",
                table: "collection",
                column: "uri");
        }
    }
}
