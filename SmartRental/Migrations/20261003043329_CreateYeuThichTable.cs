using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class CreateYeuThichTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous AddYeuThich file was not discoverable by EF because its
            // designer metadata was missing, while the model snapshot already
            // contained this entity.  Keep this repair idempotent: it is safe for
            // an existing database where the table was created outside EF.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[YeuThichs]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[YeuThichs] (
                        [UserId] nvarchar(450) NOT NULL,
                        [PhongtroId] int NOT NULL,
                        [NgayThem] datetime2 NOT NULL,
                        CONSTRAINT [PK_YeuThichs] PRIMARY KEY ([UserId], [PhongtroId]),
                        CONSTRAINT [FK_YeuThichs_AspNetUsers_UserId]
                            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_YeuThichs_Phongtros_PhongtroId]
                            FOREIGN KEY ([PhongtroId]) REFERENCES [dbo].[Phongtros] ([Id]) ON DELETE CASCADE
                    );

                    CREATE INDEX [IX_YeuThichs_PhongtroId]
                        ON [dbo].[YeuThichs] ([PhongtroId]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately leave user favorites intact if a migration rollback is
            // ever requested; this repair migration must not discard user data.
        }
    }
}
