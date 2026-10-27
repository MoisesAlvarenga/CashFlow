using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashFlow.Consolidation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ApiClients"" (
                    ""Id"" uuid NOT NULL,
                    ""ClientId"" character varying(100) NOT NULL,
                    ""SecretHash"" character varying(256) NOT NULL,
                    ""Salt"" character varying(128) NOT NULL,
                    ""Name"" character varying(150) NOT NULL,
                    ""Roles"" character varying(100) NOT NULL,
                    ""IsActive"" boolean NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    CONSTRAINT ""PK_ApiClients"" PRIMARY KEY (""Id"")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ApiClients_ClientId"" ON ""ApiClients"" (""ClientId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ApiClients"";");
        }
    }
}
