# Plataforma Multioperador de Distribución de Última Milla

Plataforma SaaS multioperador para la gestión de distribución de última milla. Un único
despliegue atiende a varios operadores logísticos, cada uno con su marca, zonas, tarifas,
reglas y usuarios, sin acceso a los datos de los demás.

Laboratorio de Taller de Sistemas de Información .NET — UTEC, Tecnólogo en Informática, 2026.
Equipo: Juliana Méndez, Martina Castro, Matías Alfaro y Cecilia Méndez.

## Aplicaciones

| Aplicación | Tecnología | Estado |
|---|---|---|
| Backoffice del operador | ASP.NET Core Razor Pages | envíos, rutas, personal, vehículos, tarifas, reglas, reportes |
| Portal del comercio | Blazor InteractiveServer | altas, importación, seguimiento, devoluciones, retiros |
| API | Minimal API + OpenAPI | interna y pública (`/api/v1`), dos instancias |
| Aplicación del repartidor | .NET MAUI (MVVM), Android | ruta del día, entregas y retiros sin conexión |
| Seguimiento público | Blazor SSR | consulta del estado con un enlace |
| Procesamiento en segundo plano | Worker .NET | mensajería, avisos a comercios y reintentos |

Monolito modular en .NET 10 para API y web, más el worker desplegado de forma independiente
que se comunica con la API solo de manera asíncrona, a través de la cola de mensajes.

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
| Documentación de la API pública (Scalar) | http://api.localhost:8000/docs |
| Contrato OpenAPI interno | http://api.localhost:8000/openapi/v1.json (requiere una sesión iniciada en el Backoffice) |
| Grafana: tablero técnico, trazas y logs | http://grafana.localhost:8000 |
| Panel de RabbitMQ | http://rabbit.localhost:8000 |
| Panel de Traefik | http://localhost:8081 |

Los navegadores resuelven `*.localhost` a la propia máquina, no hay que tocar el archivo
`hosts`. Para borrar los datos y empezar de cero: `docker compose down -v`.

### Qué levanta el compose

Catorce contenedores: `traefik` (único proxy inverso, balanceo y rate limit), `postgres`,
`redis`, `rabbitmq`, `otel-collector`, `lgtm` (Grafana con Prometheus, Tempo y Loki),
`inicializador`, **dos réplicas de `api`**, `worker`, `portal`, `backoffice`,
`receptor-avisos` (comercio de prueba que recibe los avisos firmados) y `mailpit` (correo de
prueba).

`redis` guarda el caché, los locks y el límite de tasa compartido entre réplicas. `rabbitmq`
recibe los eventos que la API publica por el outbox y los consume el Worker.

El servicio **`inicializador`** aplica las migraciones y siembra los datos de demostración una
sola vez y termina; las réplicas de la API arrancan recién cuando salió bien
(`service_completed_successfully`) y no inicializan nada. Así no compiten dos réplicas por
migrar la misma base. El arranque en frío tarda varios minutos: esperar a que todos estén
`healthy` antes de usar el sistema.

## Compilar y probar sin Docker

```bash
dotnet build UltimaMilla.slnx
dotnet test UltimaMilla.slnx
```

Las pruebas de integración necesitan un PostgreSQL y la variable de entorno
`ULTIMAMILLA_INTEGRATION_CONNECTION` apuntando a una base llamada `ultimamilla_integracion`
(nunca usan otra). Sin la variable, esas pruebas fallan. La cobertura se verifica con
`scripts/verificar-cobertura.sh`.

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

1. `docker compose ps`: los catorce contenedores arriba, `inicializador` en `Exited (0)` y dos
   de `api`.
2. Portal: iniciar sesión como **Tienda Norte**, elegir su cuenta con **RapiEnvíos** y dar de
   alta un envío.
3. Backoffice: entrar como administrador de RapiEnvíos; el envío aparece en estado
   **Admitido**.
4. Portal: otro envío con la cuenta de **Tienda Norte** con **Logística Sur**.
5. Backoffice: entrar como administrador de cada operador; cada uno ve solo sus envíos.
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
- [Decisiones](Docs/Decisiones/) — ADR-001 a ADR-008 (arquitectura, multitenancy, sincronización,
  cambio de requerimientos, caché, banderas, outbox y Blazor)
- [Cambio de requerimientos](Docs/Cambio-Requerimientos.md)
- [Modelado](Docs/Modelado/) — arquitectura lógica, despliegue, stack tecnológico
- [Planificación](Docs/Planificacion/) — análisis y diseño, plan de trabajo

## Pruebas de carga y resiliencia

- Carga (k6): [pruebas/carga/LEEME.md](pruebas/carga/LEEME.md).
- Resiliencia: [Docs/Pruebas-Resiliencia.md](Docs/Pruebas-Resiliencia.md), con
  `scripts/prueba-resiliencia.sh` y `scripts/prueba-caida-instancia.sh`.

## Limitaciones conocidas

El detalle de lo que no quedó completo y de lo que haríamos con más tiempo está en la
sección 9 del informe técnico. Las contraseñas de PostgreSQL y de RabbitMQ tienen valores de
desarrollo; en la nube van en `.env` (ver `.env.example`) y nunca se versionan.