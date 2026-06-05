using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MedicalData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTestGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "LabResults",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TestGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NameEn = table.Column<string>(type: "text", nullable: false),
                    NameRu = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestGroupMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Keyword = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGroupMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestGroupMappings_TestGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "TestGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_GroupId",
                table: "LabResults",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TestGroupMappings_GroupId",
                table: "TestGroupMappings",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_LabResults_TestGroups_GroupId",
                table: "LabResults",
                column: "GroupId",
                principalTable: "TestGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Seed groups
            migrationBuilder.Sql("""
                INSERT INTO "TestGroups" ("Id", "Name", "NameEn", "NameRu", "Icon", "Color", "Order") VALUES
                (1, 'Ліпідний профіль',       'Lipid Panel',       'Липидный профиль',    'lipid',    '#f59e0b', 1),
                (2, 'Загальний аналіз крові', 'Complete Blood Count','Общий анализ крови', 'blood',    '#6366f1', 2),
                (3, 'Глюкоза та обмін',       'Glucose & Metabolism','Глюкоза и обмен',   'glucose',  '#10b981', 3),
                (4, 'Печінка та біохімія',    'Liver & Biochemistry','Печень и биохимия',  'liver',    '#8b5cf6', 4),
                (5, 'Гормони',                'Hormones',           'Гормоны',             'hormones', '#ec4899', 5),
                (6, 'Простата / ПСА',         'Prostate / PSA',     'Простата / ПСА',      'prostate', '#06b6d4', 6);

                SELECT setval(pg_get_serial_sequence('"TestGroups"', 'Id'), 6);
            """);

            // Seed keyword mappings (ordered: specific first)
            migrationBuilder.Sql("""
                INSERT INTO "TestGroupMappings" ("Keyword", "GroupId") VALUES
                -- Простата (6)
                ('псА', 6), ('psa', 6), ('простат-специфічн', 6),
                -- Глюкоза (3) — before гемоглобін to catch HbA1c
                ('глікозильован', 3), ('нома', 3), ('homa', 3), ('с-пептид', 3),
                ('c peptide', 3), ('hba1c', 3), ('глюкоз', 3), ('інсулін', 3),
                ('insulin', 3), ('ins)', 3),
                -- Гормони (5)
                ('тестостерон', 5), ('testosterone', 5), ('fai', 5),
                ('статеві гормони', 5), ('лютеїнізуючий', 5),
                ('cortisol', 5), ('кортизол', 5), ('естрадіол', 5),
                ('прогестерон', 5), ('fsh', 5), ('фш', 5),
                -- Печінка (4)
                ('алт', 4), (' alt', 4), ('аст', 4), (' ast', 4),
                ('ггт', 4), (' ggt', 4), ('білірубін', 4),
                ('лужна фосфатаза', 4), ('лактатдегідрогеназа', 4),
                (' ldh', 4), ('загальний білок', 4),
                -- Ліпідний профіль (1)
                ('холестерин', 1), ('тригліцерид', 1), ('лпвщ', 1),
                ('лпнщ', 1), ('лпднщ', 1), ('hdl', 1), ('ldl', 1),
                ('vldl', 1), ('non-hdl', 1), ('атерогенності', 1), ('chol', 1),
                -- ЗАК (2) — most general, last
                ('гемоглобін', 2), ('еритроцит', 2), ('гематокрит', 2),
                ('лейкоцит', 2), ('нейтрофіл', 2), ('мієлоцит', 2),
                ('лімфоцит', 2), ('моноцит', 2), ('базофіл', 2),
                ('еозинофіл', 2), ('тромбоцит', 2), ('шое', 2), ('esr', 2),
                (' wbc', 2), (' rbc', 2), (' plt', 2), ('mcv', 2),
                ('mchc', 2), (' mch', 2), ('rdw', 2), ('mpv', 2),
                (' pct', 2), ('pdw', 2), ('віроцит', 2), ('плазматичн', 2);
            """);

            // Backfill GroupId for existing LabResults using keyword matching
            migrationBuilder.Sql("""
                UPDATE "LabResults" lr
                SET "GroupId" = matched."GroupId"
                FROM (
                    SELECT DISTINCT ON (lr2."Id")
                        lr2."Id",
                        m."GroupId"
                    FROM "LabResults" lr2
                    JOIN "TestGroupMappings" m
                      ON lower(lr2."TestName") LIKE '%' || lower(m."Keyword") || '%'
                    ORDER BY lr2."Id", m."Id"
                ) matched
                WHERE lr."Id" = matched."Id";
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabResults_TestGroups_GroupId",
                table: "LabResults");

            migrationBuilder.DropTable(
                name: "TestGroupMappings");

            migrationBuilder.DropTable(
                name: "TestGroups");

            migrationBuilder.DropIndex(
                name: "IX_LabResults_GroupId",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "LabResults");
        }
    }
}
