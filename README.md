# Plataforma Multioperador de Distribución de Última Milla

Plataforma SaaS multioperador para la gestión de distribución de última milla. Un único
despliegue atiende a varios operadores logísticos, cada uno con su marca, zonas, tarifas,
reglas y usuarios, sin acceso a los datos de los demás.

Laboratorio de Taller de Sistemas de Información .NET — UTEC, Tecnólogo en Informática, 2026.

## Aplicaciones

| Aplicación | Tecnología |
|---|---|
| Backoffice del operador | ASP.NET Core Razor Pages |
| Portal del comercio | Blazor |
| Seguimiento público | Blazor (sin autenticación) |
| Aplicación del repartidor | .NET MAUI (operación sin conectividad) |
| Procesamiento en segundo plano | Worker .NET |

Monolito modular en .NET 10 para API y web, más el worker desplegado de forma independiente
que se comunica solo de manera asíncrona a través de la cola de mensajes.

## Documentación

- [Letra del laboratorio](Docs/Letra-Laboratorio.md) — requerimientos y cronograma
- [Modelado](Docs/Modelado/) — arquitectura, despliegue, modelo de dominio, stack y entorno
- [Decisiones](Docs/Decisiones/) — máquina de estados, multitenancy y ADRs
- [Planificación](Docs/Planificacion/) — plan de trabajo, asignaciones y sprints

## Ejecución local

Pendiente. El entorno completo se levantará con `docker compose up`.


# Plataforma Multioperador de Distribución de Última Milla

Laboratorio de Taller de Sistemas de Información .NET — UTEC 2026.
Equipo: Juliana Méndez, Martina Castro, Matías Alfaro y Cecilia Méndez.

**Esqueleto ejecutable para el monitoreo del jueves 8/10**, alineado con el informe de análisis y diseño:
un envío dado de alta en el Portal del comercio aparece en el Backoffice, todo levanta con
`docker compose up` detrás de Traefik, y el pipeline de GitHub Actions compila y corre las pruebas.

## Cómo levantarlo

Requisito: Docker Desktop. Para compilar fuera de Docker, además el SDK de .NET 10.

```bash
docker compose up --build
```

| Aplicación | URL |
| --- | --- |
| Portal del comercio (Blazor InteractiveServer) | http://portal.localhost:8000 |
| Backoffice del operador (Razor Pages) | http://backoffice.localhost:8000 |
| API | http://api.localhost:8000/api/envios |
| Salud de la API | http://api.localhost:8000/health · `/ready` · `/live` |
| Contrato OpenAPI | http://api.localhost:8000/openapi/v1.json |
| Panel de Traefik | http://localhost:8081 |

Los navegadores resuelven `*.localhost` a la propia máquina, no hay que tocar el archivo hosts.
Para borrar los datos y empezar de cero: `docker compose down -v`.

## Compilar y probar sin Docker

```bash
dotnet build UltimaMilla.slnx
dotnet test UltimaMilla.slnx
```

## Estructura (ADR-001: Clean Architecture + Vertical Slice)

```
src/
  UltimaMilla.Domain/            No depende de nada.
    Common/          AuditableEntity, ITenantEntity
    Organizacion/    Operador, Comercio, CuentaComercial
    Envios/          Envio, Bulto, Destinatario, Direccion, EstadoEnvio, Modalidad
  UltimaMilla.Application/       Casos de uso. Depende solo de Domain.
    Abstractions/    IUnitOfWork, IEnvioRepository, ICuentaComercialRepository, IOperadorRepository
    Envios/Commands/CrearEnvio/            Command, Handler, Validator
    Envios/Queries/ListarEnvios/           Query, Handler
    CuentasComerciales/Queries/...         Query, Handler
    Operadores/Queries/...                 Query, Handler
  UltimaMilla.Infrastructure/    EF Core + PostgreSQL.
    Persistence/     AppDbContext, Configurations, Interceptors (auditoría), Seed (DbSeeder)
    Repositories/    Implementaciones de los contratos + UnitOfWork
  UltimaMilla.Api/               Minimal API, ProblemDetails, /health /ready /live
  UltimaMilla.Web.Portal/        Blazor InteractiveServer. Llama a la API por HTTP.
  UltimaMilla.Web.Backoffice/    Razor Pages. Llama a la API por HTTP.
tests/
  UltimaMilla.UnitTests/           Reglas de Envio y del validador
  UltimaMilla.Architecture.Tests/  NetArchTest: Domain y Application no dependen de EF Core ni de ASP.NET
```

## Datos de demostración (DbSeeder)

Dos operadores con configuración distinta (RapiEnvíos y Logística Sur) y dos comercios.
**Tienda Norte trabaja con los dos operadores**: tiene dos `CuentaComercial`, y sus envíos
quedan separados según con qué operador los cargue (informe 3.2 y ADR-002).

## Guion de la demo del jueves (≈ 3 minutos)

1. `docker compose up --build` ya levantado; mostrar `docker compose ps` con los 5 contenedores.
2. Abrir el Portal, elegir **Tienda Norte → RapiEnvíos** y dar de alta un envío.
3. Abrir el Backoffice: el envío aparece en estado **Admitido**.
4. Volver al Portal, dar de alta otro con **Tienda Norte → Logística Sur**.
5. En el Backoffice, filtrar por operador: cada uno ve solo el suyo.
6. Intentar repetir la misma referencia en la misma cuenta: la API responde 409 (importación idempotente, RF 7).
7. Mostrar en GitHub, pestaña Actions, el pipeline en verde (compilación, pruebas, imágenes).

## Recorrido de un envío por las capas

1. Portal → `EnviosApiClient` hace `POST /api/envios`.
2. API → arma un `CrearEnvioCommand` y llama a `CrearEnvioHandler`.
3. Handler → valida con FluentValidation, busca la `CuentaComercial`, controla la referencia duplicada,
   crea el `Envio` (que hereda el `OperadorId` de la cuenta) y guarda con el Unit of Work.
4. Infrastructure → el interceptor completa `CreadoEn`; EF Core escribe en PostgreSQL.
5. Backoffice → `GET /api/envios?operadorId=...` y muestra la tabla.

Los errores salen como ProblemDetails: 400 datos inválidos, 404 cuenta inexistente, 409 referencia repetida.

## Pendientes conocidos (no son del 8/10)

- **Migraciones (15/10):** hoy la base se crea con `EnsureCreated`. Generar la inicial con
  `dotnet tool restore` y `dotnet ef migrations add Inicial -p src/UltimaMilla.Infrastructure -s src/UltimaMilla.Api`,
  y cambiar `EnsureCreatedAsync` por `MigrateAsync` en `Infrastructure/DependencyInjection.cs`.
- **Multitenancy real (15/10):** login con ASP.NET Core Identity, `ITenantContext`, filtros globales e
  interceptor de `OperadorId`. Hoy el operador y la cuenta se eligen en pantalla (marcado como provisorio).
- Máquina de estados con Stateless (15/10), Redis, RabbitMQ con MassTransit v8, Worker, Seguimiento,
  observabilidad con OpenTelemetry y Aspire dashboard, segunda réplica de la API: según el cronograma del informe.
- Scalar para navegar la API: se agrega con el opcional de API pública.
- La contraseña de PostgreSQL tiene un valor de desarrollo; en la nube va en `.env` (ver `.env.example`).
