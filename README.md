# Plataforma Multioperador de Distribución de Última Milla

Plataforma SaaS multioperador para la gestión de distribución de última milla. Un único
despliegue atiende a varios operadores logísticos, cada uno con su marca, zonas, tarifas,
reglas y usuarios, sin acceso a los datos de los demás.

Laboratorio de Taller de Sistemas de Información .NET — UTEC, Tecnólogo en Informática, 2026.
Equipo: Juliana Méndez, Martina Castro, Matías Alfaro y Cecilia Méndez.

## Aplicaciones

| Aplicación | Tecnología | Estado |
|---|---|---|
| Backoffice del operador | ASP.NET Core Razor Pages | listado de envíos |
| Portal del comercio | Blazor InteractiveServer | alta de envíos |
| API | Minimal API + OpenAPI | 4 endpoints, dos instancias |
| Aplicación del repartidor | .NET MAUI (MVVM) | lee operadores y envíos |
| Seguimiento público | Blazor SSR | pendiente |
| Procesamiento en segundo plano | Worker .NET | pendiente |

Monolito modular en .NET 10 para API y web, más el worker desplegado de forma independiente
que se comunicará solo de manera asíncrona a través de la cola de mensajes.

## Cómo levantarlo

Requisito: Docker. Para compilar fuera de Docker, además el SDK de .NET 10.

```bash
docker compose up --build
```

| Servicio | URL |
| --- | --- |
| Portal del comercio | http://portal.localhost:8000 |
| Backoffice del operador | http://backoffice.localhost:8000 |
| API | http://api.localhost:8000/api/envios |
| Salud de la API | http://api.localhost:8000/health · `/ready` · `/live` |
| Contrato OpenAPI | http://api.localhost:8000/openapi/v1.json |
| Observabilidad (Aspire) | http://aspire.localhost:8000 |
| Panel de RabbitMQ | http://rabbit.localhost:8000 |
| Panel de Traefik | http://localhost:8081 |

Los navegadores resuelven `*.localhost` a la propia máquina, no hay que tocar el archivo
`hosts`. Para borrar los datos y empezar de cero: `docker compose down -v`.

### Qué levanta el compose

Once contenedores: `traefik` (único proxy inverso, TLS, balanceo y rate limit), `postgres`,
`redis`, `rabbitmq`, `otel-collector`, `aspire-dashboard`, `inicializador`, **dos réplicas de
`api`**, `portal` y `backoffice`.

`redis` y `rabbitmq` todavía no los consume ningún código: están declarados para que el
entorno coincida con `Docs/Modelado/stack-tecnologico.md` y para que el caché (22/10) y el
outbox con el Worker (29/10) entren sin tocar infraestructura.

El servicio **`inicializador`** crea el esquema y siembra los datos de demostración una sola
vez y termina; las réplicas de la API arrancan recién cuando salió bien
(`service_completed_successfully`) y no inicializan nada. Con dos réplicas sembrando en
paralelo contra una base vacía se viola el índice único, y ahí es donde entra `MigrateAsync`
el 15/10.

## Compilar y probar sin Docker

```bash
dotnet build UltimaMilla.slnx
dotnet test UltimaMilla.slnx
```

La app MAUI es una solución aparte y no está en `UltimaMilla.slnx` ni en el CI:

```bash
dotnet build src/UltimaMilla.Mobile/UltimaMilla.Mobile.slnx -f net10.0-android
```

## Estructura

Clean Architecture + Vertical Slice. El árbol completo y su fundamento están en
[ADR-001](Docs/Decisiones/ADR-001-estilo-arquitectonico.md); la regla de dependencias
`Domain ← Application ← Infrastructure` la verifica `tests/UltimaMilla.Architecture.Tests`
en el pipeline.

## Datos de demostración

`Persistence/Seed/DbSeeder.cs` siembra de forma idempotente, con GUID fijos, dos operadores
con configuración distinta (RapiEnvíos y Logística Sur) y dos comercios. **Tienda Norte
trabaja con los dos operadores**: tiene dos `CuentaComercial`, y sus envíos quedan separados
según con qué operador los cargue ([ADR-002](Docs/Decisiones/ADR-002-estrategia-multitenancy.md)).

## Guion de la demo

1. `docker compose ps`: los once contenedores arriba, `inicializador` en `Exited (0)` y dos
   de `api`.
2. Portal: elegir **Tienda Norte → RapiEnvíos** y dar de alta un envío.
3. Backoffice: el envío aparece en estado **Admitido**.
4. Portal: otro envío con **Tienda Norte → Logística Sur**.
5. Backoffice: filtrar por operador; cada uno ve solo el suyo.
6. Repetir la misma referencia en la misma cuenta: la API responde 409 (importación
   idempotente, RF 7).
7. `docker stop <una réplica de api>` con el Backoffice recargando: el servicio no se corta
   (RNF 6.13).
8. GitHub, pestaña Actions: el pipeline en verde.

## Recorrido de un envío por las capas

1. Portal → `EnviosApiClient` hace `POST /api/envios` contra Traefik.
2. API → arma un `CrearEnvioCommand` y llama a `CrearEnvioHandler`.
3. Handler → valida con FluentValidation, busca la `CuentaComercial`, controla la referencia
   duplicada, crea el `Envio` (que hereda el `OperadorId` de la cuenta) y guarda con el
   Unit of Work.
4. Infrastructure → el interceptor completa `CreadoEn`; EF Core escribe en PostgreSQL.
5. Backoffice → `GET /api/envios?operadorId=...` y muestra la tabla.

Los errores salen como ProblemDetails: 400 datos inválidos, 404 cuenta inexistente, 409
referencia repetida.

## Documentación

- [Letra del laboratorio](Docs/Letra-Laboratorio.md) — requerimientos y cronograma
- [Decisiones](Docs/Decisiones/) — ADR-001 (estilo arquitectónico), ADR-002 (multitenancy)
- [Modelado](Docs/Modelado/) — arquitectura lógica, despliegue, stack tecnológico
- [Planificación](Docs/Planificacion/) — análisis y diseño, plan de trabajo

## Pendientes conocidos

- **Migraciones (15/10):** hoy el esquema se crea con `EnsureCreated` en el servicio
  `inicializador`. Generar la inicial con `dotnet tool restore` y
  `dotnet ef migrations add Inicial -p src/UltimaMilla.Infrastructure -s src/UltimaMilla.Api`,
  y cambiar `EnsureCreatedAsync` por `MigrateAsync`.
- **Multitenancy real (15/10):** login con ASP.NET Core Identity, `ITenantContext`, filtros
  globales de consulta e interceptor del `OperadorId`. Hoy el operador y la cuenta se eligen
  en pantalla, y `GET /api/envios` sin `operadorId` devuelve los de todos los operadores.
- Máquina de estados con Stateless (15/10); caché en Redis con su métrica (22/10); outbox con
  MassTransit, Worker y avisos a comercios (29/10); Seguimiento público; observabilidad
  completa con Serilog y OpenTelemetry; despliegue en la nube con OpenTofu (5/11).
- Scalar para navegar la API: entra con el opcional de API pública.
- La contraseña de PostgreSQL y la de RabbitMQ tienen valores de desarrollo; en la nube van en
  `.env` (ver `.env.example`).
