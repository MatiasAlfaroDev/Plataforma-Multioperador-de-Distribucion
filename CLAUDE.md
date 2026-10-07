# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Laboratorio de Taller de Sistemas de Información .NET (UTEC 2026): plataforma SaaS
multioperador de distribución de última milla. Todo el código, los comentarios y los
documentos están en español; los tipos de framework conservan su nombre en inglés
(`Handler`, `Command`, `Query`, `Repository`).

## Comandos

```bash
dotnet build UltimaMilla.slnx                      # compila src/ y tests/ (NO incluye la app MAUI)
dotnet test UltimaMilla.slnx                       # unitarias + arquitectura
dotnet test tests/UltimaMilla.UnitTests            # un solo proyecto de pruebas
dotnet test UltimaMilla.slnx --filter "FullyQualifiedName~Crear_envio_valido"   # una sola prueba

docker compose up --build                          # entorno completo tras Traefik (puerto 8000)
docker compose down -v                             # borra también el volumen de PostgreSQL
```

Entrypoints detrás de Traefik: `portal.localhost:8000`, `backoffice.localhost:8000`,
`api.localhost:8000`, `aspire.localhost:8000` (observabilidad) y `rabbit.localhost:8000`
(panel de RabbitMQ). El panel de Traefik va en `localhost:8081`. Los navegadores resuelven
`*.localhost` solos.

### App móvil (solución aparte)

`src/UltimaMilla.Mobile` **no está en `UltimaMilla.slnx`** ni la toca el CI. Se compila por
separado y en Linux solo apunta a Android:

```bash
dotnet build src/UltimaMilla.Mobile/UltimaMilla.Mobile.slnx -f net10.0-android
```

### Migraciones de EF Core

La herramienta está declarada en `.config/dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> -p src/UltimaMilla.Infrastructure -s src/UltimaMilla.Api
```

Hoy el esquema se crea con `EnsureCreatedAsync` en `Infrastructure/DependencyInjection.cs`
(`InicializarBaseDeDatosAsync`); al generar la primera migración hay que cambiarlo por
`MigrateAsync`. Eso **no** lo corre la API: lo corre el servicio `inicializador` del compose,
que es la misma imagen con `Inicializacion__SoloInicializar=true` y termina al sembrar
(`Api/Program.cs`). Las réplicas de la API no inicializan nada.

## Arquitectura

Clean Architecture + Vertical Slice (ADR-001). Regla de dependencias
`Domain ← Application ← Infrastructure`, **verificada por pruebas**: `tests/UltimaMilla.Architecture.Tests`
falla el pipeline si `Domain` o `Application` referencian EF Core, ASP.NET Core o una capa
externa. No agregues esas dependencias a esos dos proyectos.

- **Domain** — agregados con setters privados y factorías estáticas (`Envio.Crear`,
  `CuentaComercial.Crear`). El estado solo cambia por métodos del propio agregado, y las
  invariantes se validan ahí dentro lanzando `ArgumentException` / `InvalidOperationException`.
- **Application** — un directorio por slice: `<Area>/Commands|Queries/<Caso>/` con `Command`/`Query`,
  `Handler` y, si corresponde, `Validator` de FluentValidation. Las interfaces de
  persistencia viven en `Abstractions/`; los DTO, junto al área.
- **Infrastructure** — EF Core + PostgreSQL. `AppDbContext` recoge las configuraciones con
  `ApplyConfigurationsFromAssembly`, así que una nueva entidad solo necesita su
  `IEntityTypeConfiguration` en `Persistence/Configurations/`.
- **Api** — Minimal API. Todos los endpoints se declaran inline en `Program.cs` sobre el grupo
  `/api`; no hay controladores.
- **Web.Portal** (Blazor InteractiveServer) y **Web.Backoffice** (Razor Pages) **no** referencian
  Application ni Infrastructure: hablan con la API por HTTP a través de su propio
  `Services/EnviosApiClient`, configurado con `Api:BaseUrl`.

### Convenciones que hay que respetar al agregar código

- **No hay MediatR.** Los Handlers se instancian por DI y se registran **a mano** en
  `Application/DependencyInjection.cs` (`AddScoped<MiHandler>()`), y el endpoint los recibe
  como parámetro. Un slice nuevo sin ese registro falla en tiempo de ejecución.
- **Errores:** el Handler lanza; el endpoint no tiene `try/catch`. `Api/Errores/ManejadorDeErrores.cs`
  traduce a ProblemDetails: `ValidationException` → 400, `NoEncontradoException` → 404,
  `ConflictoException` → 409, `ArgumentException`/`InvalidOperationException` del dominio → 400.
  Las dos excepciones propias están en `Application/Common/Excepciones.cs`.
- **Tiempo:** nunca `DateTime.Now`. Se inyecta `TimeProvider` y la hora se pasa al dominio
  como parámetro (`Envio.Crear(..., ahora)`), que es lo que hace comprobables las pruebas.
- **Auditoría:** `CreadoEn`/`ActualizadoEn` los completa `AuditableEntityInterceptor`; no se
  asignan en los Handlers.
- **Paquetes NuGet:** gestión central de versiones. La versión va en `Directory.Packages.props`
  de la raíz y el `.csproj` referencia el paquete **sin** `Version`. El TargetFramework
  (`net10.0`) viene de `Directory.Build.props` de la raíz.
  `src/UltimaMilla.Mobile/` tiene **sus propios** `Directory.Build.props` y
  `Directory.Packages.props` que anulan los de la raíz a propósito (MAUI necesita varios
  TargetFrameworks y versiones por `PackageReference`); no los borres.

### Multitenancy

El tenant es el **operador**. El eslabón es `CuentaComercial` (operador + comercio): un
comercio puede trabajar con varios operadores y tiene una cuenta por cada uno. `Envio` cuelga
de la cuenta y **hereda su `OperadorId`**; las entidades con tenant implementan `ITenantEntity`.
Ver ADR-002.

Provisorio hasta el hito del 15/10: no hay login, y el operador y la cuenta se eligen en
pantalla (`GET /api/cuentas-comerciales` existe solo para eso). Falta el `ITenantContext`,
los filtros globales de consulta y el interceptor que asigne y valide `OperadorId`.

### Datos de demostración

`Persistence/Seed/DbSeeder.cs` siembra de forma idempotente, con **GUID fijos**, dos operadores
(RapiEnvíos, Logística Sur) y dos comercios. *Tienda Norte* tiene cuenta con los dos operadores:
es el caso que demuestra el aislamiento entre tenants.

## Docker

Los tres `Dockerfile` se construyen **desde la raíz del repo** (`context: .` en el compose) y
copian todo el árbol, así que las rutas dentro son `src/<Proyecto>/...`. Los contenedores
escuchan en 8080 (`ASPNETCORE_HTTP_PORTS`); solo Traefik publica puertos. El router de la API
responde a `Host(api.localhost)` **y** a `PathPrefix(/api)`, esto último para que entre la app
móvil (emulador Android: `10.0.2.2:8000`, ver `Mobile/Data/Constants.cs`).

Once contenedores: `traefik`, `postgres`, `redis`, `rabbitmq`, `otel-collector`,
`aspire-dashboard`, `inicializador`, **dos réplicas de `api`**, `portal` y `backoffice`.
`redis` y `rabbitmq` están declarados pero ningún código los usa todavía (caché 22/10,
outbox 29/10). `api` e `inicializador` comparten `image: ultimamilla-api:local`, así que la
imagen se construye una sola vez; `api` no lleva `container_name` a propósito, porque con uno
fijo Docker no puede crear la segunda réplica. El Portal y el Backoffice salen por
`Api__BaseUrl: http://traefik/` y no por `api:8080`, para usar el balanceador con health
checks activos en lugar del round-robin del DNS de Docker.

**Nunca** referenciar el csproj de Mobile desde el grafo de la API, el Portal o el Backoffice:
los tres `docker build` fallarían con `NETSDK1147`, porque la imagen `sdk:10.0` no trae el
workload de MAUI. La dependencia va siempre de Mobile hacia afuera.

## Documentación

`Docs/Informe.pdf` es la **entrega de análisis y diseño** (25 páginas, 4/10/2026) y la fuente de
verdad del diseño. Sus secciones son arquitectura lógica (1), arquitectura de despliegue previsto
(2), modelo de dominio preliminar (3), máquina de estados del envío (4), estrategia de
multitenancy (5), stack tecnológico y entorno (6), plan de trabajo (7) y los dos registros de
decisión: **ADR-001** (8) y **ADR-002** (9). Se lee con `pdftotext -layout Docs/Informe.pdf -`.
`Docs/Informe.tex` es su fuente LaTeX y hoy está **vacío**: el PDF se compiló fuera del repo.

`Docs/Letra-Laboratorio.md` es la letra del laboratorio y la fuente de los requisitos; los
comentarios del código citan sus números (RF 7, RNF 6.13) y las secciones del informe.

`Docs/Planificacion/spec-inicial.md` lleva el mismo contenido del informe en Markdown, pero de la
línea 289 en adelante son tres imágenes en base64 que pesan 350 KB; hay que leer solo
`sed -n '1,288p' Docs/Planificacion/spec-inicial.md`.

`Docs/Modelado/` tiene `stack-tecnologico.md` —fuente de verdad del stack— y
`diagrama-arquitectura.md`, más `img/` con los cuatro diagramas del informe exportados a PNG:
`arquitectura-logica`, `modelo-dominio`, `despliegue` y `maquina-estados`.

Los ADR de `Docs/Decisiones/` y el resto de `Docs/` son archivos vacíos reservados; mientras sigan
así, el texto de ADR-001 y ADR-002 está en el informe (secciones 8 y 9).

Los diagramas son Mermaid con `classDef` por categoría y leyenda en tabla; la información va
en los nodos, no en las flechas.
