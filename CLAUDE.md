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
`api.localhost:8000`, `grafana.localhost:8000` (Grafana: tablero técnico, trazas y logs),
`rabbit.localhost:8000` (panel de RabbitMQ), `receptor.localhost:8000` (receptor de prueba de
los avisos; `/avisos` recibe el contrato v1 y `/avisos/v2` el v2; `POST /modo/{ok|falla|intermitente|lento}`
cambia su modo de falla) y `mail.localhost:8000` (Mailpit: los correos con el enlace de seguimiento que
recibe el destinatario). El panel de Traefik va en `localhost:8081`.
Los navegadores resuelven `*.localhost` solos.

### Estilos de Portal y Backoffice (Tailwind v4)

Los dos sitios usan Tailwind v4 con el binario standalone (sin Node), v4.1.11. El tema compartido
(tokens, tipografía, componentes) está en `src/estilos-web/tema.css`, y cada sitio tiene su entrada
en `Estilos/app.css`. **El CSS compilado (`wwwroot/app.css`) se commitea**, así los Dockerfile y el
CI no lo compilan; hay que regenerarlo después de tocar markup o el tema:

```bash
tailwindcss -i src/UltimaMilla.Web.Portal/Estilos/app.css -o src/UltimaMilla.Web.Portal/wwwroot/app.css --minify
tailwindcss -i src/UltimaMilla.Web.Backoffice/Estilos/app.css -o src/UltimaMilla.Web.Backoffice/wwwroot/app.css --minify
```

Marca compartida con la app móvil en `assets/marca/`: logo, IBM Plex Sans (texto), Space Grotesk
(títulos y cifras) e íconos Phosphor. Sin emojis en la interfaz; los íconos son `<i class="ph ph-...">`
en web y `app:Iconos` en MAUI.

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
- **Worker** — Worker Service sin HTTP. Los consumidores de MassTransit viven acá (Application no
  puede depender de MassTransit), se registran con el parámetro `consumidores` de `AddInfrastructure`
  y llaman a Handlers. Cada mensaje trae su `OperadorId` y el consumidor lo fija en
  `TenantDelMensaje` (el `ITenantContext` del Worker) **antes** de llamar al Handler; sin eso los
  filtros por operador no devuelven nada y `TenantSaveChangesInterceptor` rechaza el guardado.
  Reintentos exponenciales y cola `*_error` se configuran en la `ConsumerDefinition`.
  Las tareas programadas viven acá y no en la API escalada (`Tareas/`, `BackgroundService`), y cada
  vuelta toma un lock en Redis (`SET NX` con vencimiento) para no correr dos veces si hay dos réplicas.
- **Eventos de estado:** ningún Handler publica los cambios de estado del envío. Cada `Envio.Aplicar`
  deja un `EventoEnvio`, y `EventosDeEstadoInterceptor` publica un `EstadoEnvioCambiado` por cada uno
  en el outbox, que es lo que convierte en avisos el Worker. El interceptor solo funciona con
  `SaveChangesAsync`.
- **Tablero en vivo (RF 29, RNF 6.10):** hub `TableroHub` en `Api/TiempoReal/`, mapeado en `/api/hubs/tablero`
  (así entra por el `PathPrefix(/api)` de Traefik desde el host del Backoffice, con la misma cookie). Cada
  conexión va al grupo de su operador, tomado del claim `operador_id`. Backplane de Redis para las dos réplicas
  y cliente con WebSockets sin negociación (no hace falta afinidad). `TableroConsumer` es el **único consumidor
  de la API**: lleva cada `EstadoEnvioCambiado` al grupo; el navegador vuelve a pedir `GET /api/tablero`.
- **Observabilidad (RNF 6.12):** Serilog en API y Worker (`Infrastructure/Observabilidad/RegistroEstructurado.cs`),
  a consola y por OTLP al Collector. `X-Correlation-Id`: la app lo manda (uno por sincronización), la API lo toma o
  genera, los filtros de MassTransit lo copian a cada mensaje y lo retoman al consumir (`Correlacion.cs`). La
  profundidad de las colas la recoge el Collector del plugin `rabbitmq_prometheus`; las sincronizaciones
  pendientes las informa la app (`PUT /api/repartidor/sincronizacion`) y el Worker las publica como métrica.
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
- **Eventos (outbox, RNF 6.8):** el Handler publica con `IPublicadorDeEventos` **antes** de `GuardarCambiosAsync`: MassTransit guarda el mensaje en la tabla `OutboxMessage` dentro de la misma transacción y lo entrega a RabbitMQ después. Los eventos son `record` en `<Area>/Eventos/`; el exchange se llama por namespace y tipo, así que moverlos rompe a quien los consuma. El bus no está en `/ready`: sin RabbitMQ la API sigue aceptando escrituras.
- **Paquetes NuGet:** gestión central de versiones. La versión va en `Directory.Packages.props`
  de la raíz y el `.csproj` referencia el paquete **sin** `Version`. El TargetFramework
  (`net10.0`) viene de `Directory.Build.props` de la raíz.
  `src/UltimaMilla.Mobile/` tiene **sus propios** `Directory.Build.props` y
  `Directory.Packages.props` que anulan los de la raíz a propósito (MAUI necesita varios
  TargetFrameworks y versiones por `PackageReference`); no los borres.

### Actores y perfiles (letra 3.3)

Los perfiles son strings en `Application/Seguridad/Perfiles.cs`, junto con los grupos que usan los
endpoints (`RequireRole(Perfiles.Despacho)`) y los handlers (`Perfiles.Deposito.Contains(...)`):

- `Configuracion`: solo `AdministradorOperador` (zonas, tarifas, reglas, personal).
- `Despacho`: AO + `Despachador` (y `Operador`, el perfil anterior, que equivale al despachador).
- `Deposito`: lo de despacho más `OperarioDeposito`.

Un perfil nuevo hay que sumarlo al grupo que corresponda y no compararlo suelto. El personal del
operador se da de alta en `/api/personal` y los usuarios del comercio en `/api/comercio/usuarios`
(`Api/Endpoints/PersonalEndpoints.cs`). Los usuarios del comercio entran al Portal por `UsuarioComercio`
y no tienen operador en la sesión: los handlers del comercio fijan el operador de la cuenta con
`IOperadorActivo.SeleccionarOperador` después de validarla. El sistema del comercio puede llamar a la API
con la clave del encabezado `X-Api-Key` (`ClaveApiAuthenticationHandler`), que no lleva token
antifalsificación. El destinatario no tiene cuenta: si el envío tiene correo, el Worker le manda el
enlace de seguimiento (`EnlaceSeguimientoSolicitado`).

### Multitenancy

El tenant es el **operador**. El eslabón es `CuentaComercial` (operador + comercio): un
comercio puede trabajar con varios operadores y tiene una cuenta por cada uno. `Envio` cuelga
de la cuenta y **hereda su `OperadorId`**; las entidades con tenant implementan `ITenantEntity`.
Ver ADR-002.

El `ITenantContext` de la API sale de los claims de la cookie (`HttpTenantContext`). Los filtros
globales por `OperadorId` están en `AppDbContext`, y `TenantSaveChangesInterceptor` rechaza guardar
una entidad de otro operador.

### Caché y banderas (aclaraciones 3.1 y 3.2)

- **Caché.** Todo pasa por `ICacheService` (Redis). Las claves se arman en `ClavesCache`: el primer
  segmento es el caso de uso, que es la etiqueta de las métricas, y las de configuración llevan el
  operador. Sin Redis, cada operación cuenta como fallo y se sigue contra la base. El Worker invalida el
  seguimiento público al procesar los eventos del envío. Ver ADR-005.
- **Banderas.** `IBanderas` con las constantes de `Banderas` (`entrega-parcial`, `retiro-domicilio`). Las
  cambia el `AdministradorPlataforma` desde el Backoffice y cada cambio queda auditado. Una funcionalidad
  nueva bajo bandera tiene que consultarla en la API, el Worker, las webs y la app. Ver ADR-006.

### Datos de demostración

`Persistence/Seed/DbSeeder.cs` siembra de forma idempotente, con **GUID fijos**, dos operadores
(RapiEnvíos, Logística Sur) y dos comercios. *Tienda Norte* tiene cuenta con los dos operadores:
es el caso que demuestra el aislamiento entre tenants.

## Docker

Los cinco `Dockerfile` se construyen **desde la raíz del repo** (`context: .` en el compose) y
copian todo el árbol, así que las rutas dentro son `src/<Proyecto>/...`. Los contenedores
escuchan en 8080 (`ASPNETCORE_HTTP_PORTS`); solo Traefik publica puertos. El router de la API
responde a `Host(api.localhost)` **y** a `PathPrefix(/api)`, esto último para que entre la app
móvil (emulador Android: `10.0.2.2:8000`, ver `Mobile/Data/Constants.cs`).

Catorce contenedores: `traefik`, `postgres`, `redis`, `rabbitmq`, `otel-collector`,
`lgtm`, `inicializador`, **dos réplicas de `api`**, `worker`, `receptor-avisos`, `mailpit`, `portal` y `backoffice`.
`rabbitmq` recibe los eventos del outbox y el `worker` los consume; `receptor-avisos` es el sistema
de comercio de prueba al que apunta la suscripción sembrada para Tienda Norte en RapiEnvíos. `api` e `inicializador` comparten `image: ultimamilla-api:local`, así que la imagen se construye una sola vez; `api` no lleva `container_name` a propósito, porque con uno fijo Docker no puede crear la segunda réplica. El Portal y el Backoffice salen por
`Api__BaseUrl: http://traefik/` y no por `api:8080`, para usar el balanceador con health
checks activos en lugar del round-robin del DNS de Docker.

**Nunca** referenciar el csproj de Mobile desde el grafo de la API, el Portal o el Backoffice:
los tres `docker build` fallarían con `NETSDK1147`, porque la imagen `sdk:10.0` no trae el
workload de MAUI. La dependencia va siempre de Mobile hacia afuera.

El router de la API responde a `PathPrefix(/api)`, así que todo router de otro host cuyo panel use `/api` (Grafana, RabbitMQ, Mailpit) necesita `priority=100`.

## Nube (RNF 6.14)

`infra/` es OpenTofu (`azurerm`): VM de Azure con IP fija y, opcionalmente, zona de Azure DNS. Sin
dominio propio, los hosts son `<servicio>.<ip-con-guiones>.sslip.io`. `infra/reconstruir.sh` hace
el `apply` y dispara el deploy; `infra/LEEME.md` documenta todo, incluidos secretos y costo. El job
`desplegar` de `ci.yml` entra por SSH, escribe el `.env` y corre
`docker compose -f docker-compose.yml -f docker-compose.nube.yml up -d --build`. Queda apagado
hasta que la variable `DESPLIEGUE_HABILITADO` sea `true`. En la nube, Portal y Backoffice llegan a
la API por el entrypoint interno `traefik:81`, porque el `web` (80) redirige todo a HTTPS.

`scripts/prueba-caida-instancia.sh` reproduce el escenario 1 (caída de una réplica con tráfico).
Pega a `/live` y no a `/ready`: `/ready` falla a propósito en la réplica que se apaga
(`Api/Salud/DrenajeAlApagar.cs`).

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

`Docs/Decisiones/` tiene ADR-001 a ADR-008: ADR-003 (conflictos de sincronización, también por bulto
y retiro), ADR-004 (el cambio de requerimientos), ADR-005 (caché), ADR-006 (banderas), ADR-007 (outbox:
consistencia entre la base y los eventos) y ADR-008 (modo de renderizado de Blazor).
`Docs/Cambio-Requerimientos.md` es el cambio del 30/10 (RF 31 a 41) con las aclaraciones de caché y
banderas, y prevalece sobre la letra donde difieren.

Los diagramas son Mermaid con `classDef` por categoría y leyenda en tabla; la información va
en los nodos, no en las flechas.
