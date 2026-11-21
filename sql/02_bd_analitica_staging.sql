/* =====================================================================
   BD ANALÍTICA  (destino del ETL + origen del dashboard)
   - Esquema staging con la tabla donde el DataLoader inserta (SqlBulkCopy)
   - Vistas para Power BI en el esquema dw
   - Usuarios con mínimo privilegio: etl_writer (solo INSERT en staging)
     y dashboard_reader (solo SELECT sobre las vistas)
   ===================================================================== */
IF DB_ID('OpinionesDW') IS NULL CREATE DATABASE OpinionesDW;
GO
USE OpinionesDW;
GO

IF SCHEMA_ID('staging') IS NULL EXEC('CREATE SCHEMA staging');
GO
IF SCHEMA_ID('dw') IS NULL EXEC('CREATE SCHEMA dw');
GO

IF OBJECT_ID('staging.Opinion') IS NULL
CREATE TABLE staging.Opinion (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    BatchId     UNIQUEIDENTIFIER NOT NULL,   -- ciclo ETL que cargó el registro
    Source      NVARCHAR(10)     NOT NULL,   -- CSV | DB | API
    ExternalId  NVARCHAR(100)    NULL,
    [Text]      NVARCHAR(MAX)    NOT NULL,
    Rating      INT              NULL,
    OpinionDate DATETIME2        NULL,
    ExtractedAt DATETIME2        NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_staging_Opinion_Batch')
    CREATE INDEX IX_staging_Opinion_Batch  ON staging.Opinion (BatchId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_staging_Opinion_Source')
    CREATE INDEX IX_staging_Opinion_Source ON staging.Opinion (Source, ExtractedAt);
GO

/* ---------- Vistas para el dashboard ---------- */
CREATE OR ALTER VIEW dw.vw_OpinionesPorFuente AS
SELECT Source,
       COUNT(*)                         AS TotalOpiniones,
       AVG(CAST(Rating AS FLOAT))       AS PromedioCalificacion,
       SUM(CASE WHEN Rating >= 4 THEN 1 ELSE 0 END) AS Positivas,
       SUM(CASE WHEN Rating <= 2 THEN 1 ELSE 0 END) AS Negativas
FROM staging.Opinion
GROUP BY Source;
GO

CREATE OR ALTER VIEW dw.vw_CalificacionPorDia AS
SELECT CAST(OpinionDate AS DATE)        AS Dia,
       COUNT(*)                         AS Opiniones,
       AVG(CAST(Rating AS FLOAT))       AS PromedioCalificacion
FROM staging.Opinion
WHERE OpinionDate IS NOT NULL AND Rating IS NOT NULL
GROUP BY CAST(OpinionDate AS DATE);
GO

CREATE OR ALTER VIEW dw.vw_Opiniones AS
SELECT Id, BatchId, Source, ExternalId, [Text], Rating, OpinionDate, ExtractedAt
FROM staging.Opinion;
GO

/* ---------- Seguridad: mínimo privilegio ----------
   IMPORTANTE: cambie las contraseñas y guárdelas en User Secrets / variables de entorno. */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'etl_writer')
    CREATE LOGIN etl_writer WITH PASSWORD = 'CambiarEstaClave#2026', CHECK_POLICY = ON;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'dashboard_reader')
    CREATE LOGIN dashboard_reader WITH PASSWORD = 'CambiarEstaClave#2026', CHECK_POLICY = ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'etl_writer')
    CREATE USER etl_writer FOR LOGIN etl_writer;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'dashboard_reader')
    CREATE USER dashboard_reader FOR LOGIN dashboard_reader;
GO
GRANT INSERT ON staging.Opinion TO etl_writer;
GRANT SELECT ON SCHEMA::dw TO dashboard_reader;
GO
