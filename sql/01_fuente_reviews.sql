/* =====================================================================
   BD RELACIONAL DE ORIGEN  (fuente "DB" del ETL)
   Crea la base ReviewsDB con la tabla dbo.Reviews, datos de ejemplo
   y un usuario de SOLO LECTURA para el extractor (mínimo privilegio).
   Ejecutar con un usuario administrador (sa) en SQL Server 2019+.
   ===================================================================== */
IF DB_ID('ReviewsDB') IS NULL CREATE DATABASE ReviewsDB;
GO
USE ReviewsDB;
GO

IF OBJECT_ID('dbo.Reviews') IS NULL
CREATE TABLE dbo.Reviews (
    ReviewId   INT IDENTITY(1,1) PRIMARY KEY,
    Texto      NVARCHAR(MAX) NOT NULL,
    Puntuacion INT           NULL CHECK (Puntuacion BETWEEN 1 AND 5),
    Fecha      DATETIME2     NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Reviews)
INSERT INTO dbo.Reviews (Texto, Puntuacion, Fecha) VALUES
(N'Muy buen producto, superó mis expectativas', 5, '2026-09-20'),
(N'La entrega se retrasó una semana',            2, '2026-09-22'),
(N'Excelente relación calidad-precio',           5, '2026-09-25'),
(N'El empaque llegó dañado',                     2, '2026-09-27'),
(N'Funciona bien, nada especial',                3, '2026-09-28'),
(N'Atención al cliente muy eficiente',           4, '2026-10-01'),
(N'No cumple con lo descrito en la página',      1, '2026-10-02'),
(N'Lo volvería a comprar sin dudarlo',           5, '2026-10-04'),
(N'Tuve que pedir un reembolso',                 1, '2026-10-06'),
(N'Buena experiencia general',                   4, '2026-10-08'),
(N'Instalación sencilla y rápida',               4, '2026-10-10'),
(N'La batería dura menos de lo anunciado',       2, '2026-10-12');
GO

/* ---- Usuario de solo lectura para el ETL ----
   IMPORTANTE: cambie la contraseña. No la suba a GitHub: guárdela en User Secrets. */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'etl_reader')
    CREATE LOGIN etl_reader WITH PASSWORD = 'CambiarEstaClave#2026', CHECK_POLICY = ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'etl_reader')
    CREATE USER etl_reader FOR LOGIN etl_reader;
GO
GRANT SELECT ON dbo.Reviews TO etl_reader;
GO
