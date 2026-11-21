# ETL de Opiniones de Clientes — Worker Service (.NET 8)

Proceso de **extracción (E de ETL)** que consolida opiniones desde tres fuentes heterogéneas:

| Fuente | Clase | Tecnología |
|---|---|---|
| Encuestas internas (CSV) | `CsvExtractor` | CsvHelper (streaming) |
| Reseñas en BD relacional | `DatabaseExtractor` | ADO.NET (`Microsoft.Data.SqlClient`) |
| Comentarios de API REST | `ApiExtractor` | `HttpClient` tipado + `IHttpClientFactory` |

Los datos se normalizan al modelo `Opinion`, se validan y se guardan en **archivos JSON temporales** o en la **tabla staging de la BD analítica** (`SqlBulkCopy`). Los logs usan `ILogger` con Serilog.

## Arquitectura (Clean Architecture)

```
Etl.Worker  ──►  Etl.Infrastructure  ──►  Etl.Application  ──►  Etl.Domain
(host, DI)       (CSV/BD/API, staging)    (IExtractor, orquestador)  (Opinion)
```

Las dependencias apuntan siempre hacia adentro. Agregar una fuente nueva = una clase `IExtractor` + una sección en `appsettings.json` + una línea en `DependencyInjection.cs`.

## Requisitos

- .NET 8 SDK
- (Opcional) Docker, para levantar SQL Server local con `docker compose`

## Ejecución rápida

### 1. Bases de datos locales (fuente BD y BD analítica)

```bash
cp .env.example .env            # edite MSSQL_SA_PASSWORD
docker compose up -d
# Ejecute en orden (con Azure Data Studio, SSMS o sqlcmd):
#   sql/01_fuente_reviews.sql
#   sql/02_bd_analitica_staging.sql
```

> Cambie las contraseñas de los usuarios `etl_reader`, `etl_writer` y `dashboard_reader` dentro de los scripts **antes** de ejecutarlos.

### 2. Credenciales (nunca en el repositorio)

```bash
cd src/Etl.Worker
dotnet user-secrets set "Sources:Database:ConnectionString" "Server=localhost,1433;Database=ReviewsDB;User Id=etl_reader;Password=<su-clave>;TrustServerCertificate=True;Encrypt=True"

# Solo si usa staging en SQL (Staging:Mode = Sql):
dotnet user-secrets set "Staging:Mode" "Sql"
dotnet user-secrets set "Staging:ConnectionString" "Server=localhost,1433;Database=OpinionesDW;User Id=etl_writer;Password=<su-clave>;TrustServerCertificate=True;Encrypt=True"

# Solo si su API requiere llave:
dotnet user-secrets set "Sources:Api:ApiKey" "<su-api-key>"
```

En producción use variables de entorno (`Sources__Database__ConnectionString`) o un gestor de secretos (Azure Key Vault, etc.).

### 3. Ejecutar

```bash
dotnet run --project src/Etl.Worker -- --Extraction:RunOnce=true     # un ciclo y termina
dotnet run --project src/Etl.Worker                                   # servicio continuo (cada 60 min)
```

Resultados: archivos en `src/Etl.Worker/staging/` (modo Json) o filas en `staging.Opinion` (modo Sql); logs en consola y en `src/Etl.Worker/logs/`.

Para probar solo con CSV + API (sin BD), deshabilite la fuente:
`--Sources:Database:Enabled=false`

### 4. Pruebas

```bash
dotnet test
```

## Configuración (`appsettings.json`)

| Sección | Qué controla |
|---|---|
| `Extraction` | Intervalo, `RunOnce`, paralelismo máximo, reintentos y backoff |
| `Staging` | `Json` o `Sql`, carpeta, tabla y tamaño de lote |
| `Sources:Csv / Database / Api` | Habilitar/deshabilitar cada fuente, rutas, query, URL, timeouts |
| `Serilog` | Niveles y destinos de log (consola y archivo diario) |

La configuración se valida al arrancar (`ValidateOnStart`): si falta una cadena de conexión el servicio falla de inmediato con un mensaje claro.

## Dashboard

Conecte Power BI Desktop a `OpinionesDW` con el usuario `dashboard_reader` y use las vistas `dw.vw_OpinionesPorFuente`, `dw.vw_CalificacionPorDia` y `dw.vw_Opiniones`.

## Estructura

```
src/Etl.Domain          Opinion, SourceNames
src/Etl.Application     IExtractor, IStagingWriter, ExtractionOrchestrator, OpinionValidator
src/Etl.Infrastructure  CsvExtractor, DatabaseExtractor, ApiExtractor, JsonFileStagingWriter, SqlStagingWriter, DI
src/Etl.Worker          Program.cs, Worker.cs, appsettings.json, Data/encuestas.csv
tests/Etl.Tests         Pruebas xUnit
sql/                    Scripts de BD origen y BD analítica (staging + vistas)
docs/                   Diagramas
```
