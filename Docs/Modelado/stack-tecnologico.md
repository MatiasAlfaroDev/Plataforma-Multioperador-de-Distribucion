# Stack tecnológico

Documento de referencia del stack de la plataforma: qué tecnología se usa en cada área y por
qué. Es la fuente de verdad del stack; el fundamento de fondo de cada decisión mayor vive en
su registro de decisión de arquitectura correspondiente (`Docs/Decisiones/`).

Todo el stack es .NET 10 sobre contenedores, con componentes open source que caben en el
crédito gratuito de la nube. No quedan opciones abiertas: las que en la primera versión del
análisis figuraban "a confirmar" están cerradas acá.

## 1. Stack confirmado

| Área | Elección | Motivo breve |
| :---- | :---- | :---- |
| Plataforma | .NET 10 | Exigido por la letra |
| Base transaccional | PostgreSQL 16 + EF Core 10 (Npgsql) | Mismo motor que la demo Support; índices parciales, `xmin` como token de concurrencia y Row-Level Security disponible |
| Validación | FluentValidation | Igual que la demo: un Validator por Command o Query |
| Caché y coordinación | Redis | Con dos instancias de API el `IMemoryCache` no se comparte; Redis sí. También backplane de SignalR, llaves de Data Protection y locks distribuidos |
| Mensajería | RabbitMQ | MOM con broker visto en clase; colas de trabajo con reintentos y cola de fallidos |
| Librería de mensajería y outbox | MassTransit v8 (`AddEntityFrameworkOutbox` + `UseBusOutbox`) | Outbox transaccional sobre EF Core, idempotencia por inbox, reintentos con espera creciente y cola de fallidos ya resueltos. Versión fijada en `Directory.Packages.props`: v8 es Apache-2.0, v9 cambia de licencia |
| Máquina de estados | Stateless | Transiciones declarativas, verificables y exportables a gráfico |
| Identidad | ASP.NET Core Identity | La letra lo admite como almacén de usuarios; cookies en las webs, tokens bearer en la API y la app; claims de operador y comercio |
| API | Minimal API con OpenAPI nativo + Scalar | Mismo stack que la demo de patrones |
| Backoffice del operador | ASP.NET Core Razor Pages | Exigido por la letra |
| Portal del comercio | Blazor `InteractiveServer` | Interactividad con estado en el servidor; funciona tras el balanceador con las llaves de Data Protection y el backplane de SignalR en Redis |
| Seguimiento público | Blazor SSR estático + `OutputCache` | Es la superficie de mayor tráfico y la única anónima: render estático evita un circuito y un WebSocket por visitante |
| Tiempo real | SignalR + backplane Redis | Un mensaje emitido desde una instancia llega a los clientes conectados a la otra |
| Tareas programadas | Alojadas solo en el Worker (1 réplica) + lock en Redis | La duplicación que advierte 6.13 solo aparece si se alojan en la API escalada; el lock cubre el caso de reinicio solapado |
| Móvil | .NET MAUI con CommunityToolkit.Mvvm, ZXing.Net.Maui, `Microsoft.Data.Sqlite` con `SQLitePCLRaw.bundle_e_sqlcipher`, Plugin.Fingerprint, Firebase Cloud Messaging | Mismas librerías que la demo BeatScan, cambiando el bundle de SQLite por uno con cifrado |
| Instrumentación | Serilog (registro estructurado) + OpenTelemetry (métricas y trazas) → OpenTelemetry Collector | Exigido por 6.12; el Collector desacopla la aplicación del backend de visualización |
| Visualización — desarrollo | Aspire dashboard | Logs, métricas y trazas correlacionadas en un contenedor y sin configuración |
| Visualización — nube | Prometheus + Tempo + Grafana | Sostiene el tablero técnico con paneles propios e historial, que el Aspire dashboard no da |
| Proxy inverso y TLS | Traefik + Let's Encrypt | Único proxy del sistema: balanceo con health checks activos, certificados automáticos, rate limit y cabeceras seguras (sección 2) |
| Pruebas | xUnit, Testcontainers, NetArchTest | Unitarias de dominio, integración contra Postgres y RabbitMQ reales, y la prueba de arquitectura que exige 6.1 |
| Infraestructura como código | OpenTofu + provider `azurerm` | Compatible con Terraform, sin licencia restrictiva; mantiene posible un cambio de proveedor |
| CI/CD | GitHub Actions | Compila, prueba, construye imágenes y despliega |
| Nube | Azure for Students — VM Linux `Standard_B2as_v2` | IaaS con crédito de estudiante, sin tarjeta de crédito y con corte automático al agotarse |

### Opcionales elegidos (12 puntos)

| Opcional | Tecnología | Motivo breve |
| :---- | :---- | :---- |
| API pública para comercios (3) | `Asp.Versioning` + OpenAPI/Scalar, claves por comercio, rate limit diferenciado de ASP.NET Core | Extiende el alta por API del RF 7 sobre el mismo Minimal API |
| Notificaciones remotas en producción (3) | Firebase Cloud Messaging | Ya es el canal push de la app del repartidor (6.11) |
| Pruebas de carga (2) | k6 en contenedor | Escenarios como código, se ejecuta desde el pipeline contra el ambiente remoto |
| Pruebas de resiliencia (2) | `docker compose stop` de la dependencia + Polly para el degradado; Toxiproxy si hace falta latencia o pérdida | Interrumpe caché, cola, base y receptor externo sin tocar la aplicación |
| Cobertura de pruebas (2) | coverlet + ReportGenerator, reporte publicado por GitHub Actions | Cobertura verificable sobre dominio y aplicación |

Playwright queda fuera del stack: las pruebas de extremo a extremo no están entre los
opcionales elegidos.

## 2. Traefik como único proxy inverso

Se usa un solo proxy en el borde, Traefik, y no hay contenedor nginx.

- **Descubrimiento por labels de Docker.** `api` escala a dos réplicas sin editar ningún
  upstream. Con nginx el bloque `upstream` se escribe y se recarga a mano cada vez que cambia
  la cantidad de instancias.
- **Health checks activos sobre `/ready`.** Traefik saca del pool a la instancia que falla el
  chequeo *antes* de enrutarle tráfico. nginx open source solo tiene chequeos **pasivos**
  (`max_fails`, `proxy_next_upstream`): reacciona después de haber fallado peticiones reales.
  Esto pesa directamente en el primer escenario anunciado para la defensa: interrumpir una
  instancia de la API con tráfico en curso y verificar la continuidad del servicio.
- **ACME integrado.** Certificado válido de Let's Encrypt sin contenedor `certbot`, sin cron
  de renovación y sin volumen compartido de certificados.
- **Rate limit y cabeceras seguras declarativos** como middlewares, que es parte de lo que
  exige 6.3.
- **Un contenedor en lugar de dos**, en una VM que ya corre el resto del compose.

Alternativas descartadas:

- **nginx como único proxy.** Familiar y de configuración explícita, pero obliga a un sidecar
  `certbot` con su renovación, a mantener los upstreams a mano y a conformarse con health
  checks pasivos.
- **nginx delante de Traefik.** Dos proxies que configurar, explicar y depurar. Solo se
  justificaría por una caché HTTP para el seguimiento público, que ya se resuelve con
  `OutputCache` sobre Redis dentro de la propia aplicación.
- **YARP.** Cumple la misma función en .NET, pero no hace ACME: haría falta otro componente
  para los certificados.

## 3. Observabilidad: dos backends, una sola instrumentación

La aplicación habla **únicamente OTLP contra el OpenTelemetry Collector**. El backend de
visualización es, por lo tanto, una decisión de perfil de `docker compose` y no de código: el
mismo binario instrumentado sirve a los dos.

- **En desarrollo**, el Aspire dashboard recibe ese OTLP y muestra logs, métricas y trazas
  correlacionadas en un contenedor, sin configuración ni dashboards que provisionar.
- **En la nube**, Prometheus, Tempo y Grafana sostienen el tablero técnico que exige 6.12
  (tiempo medio de respuesta, percentiles 95 y 99, tasa de errores, profundidad de la cola,
  sincronizaciones pendientes, avisos a comercios fallidos y relación de aciertos del caché).
  El Aspire dashboard no puede cubrirlo: no admite paneles propios, no persiste y se vacía al
  reiniciar.

El identificador de correlación viaja por el contexto de OpenTelemetry desde la app del
repartidor hasta el Worker, con los mensajes de MassTransit propagando el contexto de traza.

## 4. Trazabilidad contra los requerimientos no funcionales

Autocontrol del stack: cada requerimiento obligatorio de la sección 6 de la letra con los
componentes que lo cubren. Un requerimiento sin fila sería un agujero en el stack.

| RNF | Componentes del stack |
| :---- | :---- |
| 6.1 Plataforma y arquitectura | .NET 10; Docker y `docker compose` para todo el entorno, servicios de soporte incluidos; NetArchTest para la prueba de arquitectura |
| 6.2 Capa de presentación web | Razor Pages (Backoffice); Blazor `InteractiveServer` (Portal); Blazor SSR estático + `OutputCache` (Seguimiento público) |
| 6.3 Seguridad y autenticación | ASP.NET Core Identity; TLS, rate limit y cabeceras seguras en Traefik; CORS de ASP.NET Core; SQLCipher en el dispositivo; `dotnet user-secrets`, GitHub Secrets y `.env` fuera del repositorio |
| 6.4 Persistencia y datos | PostgreSQL 16 + EF Core 10 con migraciones vía `dotnet-ef`; `xmin` de Npgsql como token de concurrencia optimista; SQLite cifrado en el móvil |
| 6.5 Multitenancy | Filtros globales de EF Core alimentados por `ITenantContext` + interceptor de `SaveChanges`; Testcontainers para las pruebas de aislamiento; Row-Level Security de PostgreSQL como defensa adicional |
| 6.6 Configurabilidad y parametrización | Versiones de tarifario y de reglas en EF Core, con caché e invalidación en Redis |
| 6.7 Caché distribuido | Redis vía `IDistributedCache` y `OutputCache`; métricas de aciertos y fallos por OpenTelemetry hacia Grafana |
| 6.8 Mensajería y segundo plano | RabbitMQ + MassTransit v8: outbox sobre EF Core, inbox para idempotencia, reintentos con espera creciente y cola de fallidos; Worker Service independiente; consulta y reintento de fallidos desde el Backoffice |
| 6.9 Avisos automáticos a los comercios | `HttpClient` con Polly, firma HMAC del mensaje, reintentos y cola de fallidos de MassTransit; receptor de prueba propio con modo de falla configurable |
| 6.10 Comunicación en tiempo real | SignalR con backplane de Redis, con grupos por operador |
| 6.11 Aplicación móvil | .NET MAUI con MVVM (CommunityToolkit.Mvvm), ZXing.Net.Maui, SQLite con SQLCipher, Plugin.Fingerprint, Firebase Cloud Messaging |
| 6.12 Observabilidad | Serilog + OpenTelemetry → Collector; Aspire dashboard en desarrollo; Prometheus, Tempo y Grafana en la nube |
| 6.13 Escalabilidad y balanceo | Traefik balanceando dos instancias de API con health checks activos; `/health`, `/ready` y `/live` de ASP.NET Core; apagado controlado; Data Protection en Redis; tareas programadas alojadas solo en el Worker |
| 6.14 Despliegue e IaC | Azure for Students sobre VM `Standard_B2as_v2`; OpenTofu con provider `azurerm`; Traefik + Let's Encrypt para HTTPS válido; GitHub Actions para compilar, probar y desplegar |
| 6.15 Calidad | xUnit (dominio), Testcontainers (integración y aislamiento entre inquilinos), NetArchTest (arquitectura), coverlet + ReportGenerator, todo ejecutado por GitHub Actions |
