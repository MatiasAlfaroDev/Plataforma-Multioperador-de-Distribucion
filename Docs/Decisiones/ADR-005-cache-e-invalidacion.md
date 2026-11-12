# ADR-005 — Caché distribuida e invalidación

**Estado:** aceptada · **Fecha:** 12/11/2026

## Contexto

La letra exige caché **distribuida** y la aclaración 3.1 del cambio de requerimientos lo precisa. La API
corre en dos réplicas detrás de Traefik: una caché en memoria tendría una copia por réplica, y una
invalidación en una no llegaría a la otra. Además, la API tiene que ser sin estado.

Se evalúa:

- dos casos de uso con perfiles de acceso distintos;
- claves que distingan al operador;
- métrica de aciertos y fallos por caso de uso en el tablero técnico;
- que el sistema siga funcionando contra la base si la caché no está.

## Decisión

**Redis como único nivel**, con un `ICacheService` propio sobre StackExchange.Redis (`RedisCacheService`).
No hay copia local, así que no hay que avisar a las demás réplicas: todas leen la misma entrada.

| Caso de uso | Clave | Expiración | Invalidación |
| :-- | :-- | :-- | :-- |
| Seguimiento público | `seguimiento:{hash del token}` | 60 s | El Worker la borra al procesar cada `EstadoEnvioCambiado` o `EstadoBultoCambiado` del envío (`InvalidarSeguimientoConsumer`, con su propia cola). |
| Tarifa vigente | `tarifa-vigente:{operador}:{zona}:{servicio}` | 1 h | Publicar una versión (`ActualizarTarifa`, `CrearTarifa`) borra el prefijo del operador. |
| Reglas operativas vigentes | `reglas-vigentes:{operador}` | 1 h | Publicar una versión borra la clave. |
| Banderas de funcionalidad | `banderas:{operador}` | 15 s | Cambiar una bandera borra la clave (ADR-006). |

Fundamentos:

- **Seguimiento.** Mucho volumen, y el estado cambia varias veces por día. La invalidación por evento es
  lo que garantiza que el destinatario vea el cambio; la expiración corta es un respaldo. La clave es el
  hash del token, que pertenece a un único envío de un único operador.
- **Configuración.** Se consulta todo el tiempo y casi no cambia. Una versión publicada **no se modifica**:
  lo único que cambia es cuál está vigente, así que la invalidación es una sola y ocurre al publicar.
- **Inquilinos.** Toda clave de configuración lleva el operador (`ClavesCache`), así que una entrada
  nunca sirve a otro inquilino.
- **Sin Redis.** Cada operación atrapa el error de Redis, lo registra en el log y en la métrica
  `cache_errores_total`, y se comporta como un fallo de caché: el caso de uso va a la base. La API y el
  Worker conectan con `AbortOnConnectFail=false`, así que arrancan sin Redis y se reconectan solos. Lo
  verifica `CacheSinRedisTests`.
- **Métricas.** `cache_hits_total`, `cache_misses_total` y `cache_errores_total` son contadores de
  OpenTelemetry con la etiqueta `caso_de_uso` (primer segmento de la clave). El tablero técnico de Grafana
  los muestra por caso de uso.

El mismo razonamiento alcanza a otros mecanismos con estado compartido:

| Mecanismo | Cómo se comparte entre réplicas |
| :-- | :-- |
| Protección de datos de las cookies | Claves en Redis, con el mismo nombre de aplicación en API, Portal y Backoffice. |
| Tareas programadas | Lock `SET NX` con vencimiento en Redis (Worker). |
| Limitación de tasa | Contador por IP y minuto en Redis (`LimiteDeTasa`) para el seguimiento y el login. El limitador de ASP.NET Core cuenta por instancia y con dos réplicas duplicaría el límite. |
| SignalR | No hay hubs propios. El único SignalR es el circuito de Blazor Server del Portal, que necesita afinidad y no un backplane: Traefik la da con la cookie `UltimaMilla.Afinidad`. |

## Alternativas consideradas

| Alternativa | Por qué se descartó |
| :-- | :-- |
| `IMemoryCache` | Una copia por réplica: la otra seguiría respondiendo el valor viejo. Además es estado en memoria. |
| `HybridCache` / FusionCache (dos niveles) | Más rápido en lecturas, pero obliga a propagar las invalidaciones entre réplicas (backplane). Con nuestro volumen, un viaje a Redis alcanza. |
| `IDistributedCache` + OutputCache | `IDistributedCache` no permite borrar por prefijo, y la tarifa tiene una clave por zona y servicio. |

## Consecuencias

- Una lectura es un viaje de red a Redis. Es aceptable para el volumen del laboratorio.
- El borrado por prefijo recorre claves con `SCAN`. Sirve para decenas de claves por operador; con miles,
  convendría un contador de versión dentro de la clave.
- Si Redis no está, las cookies de las webs no pueden validarse: la protección de datos depende de Redis.
  La API sigue atendiendo a la app y al seguimiento público.

## Verificación

1. Con dos réplicas de la API, cambiar una tarifa (`POST /api/tarifas`) y consultar
   `GET /api/tarifas/vigente` varias veces: las dos réplicas devuelven el valor nuevo de inmediato.
2. `docker compose stop redis`: el seguimiento público y la API siguen respondiendo, y
   `cache_errores_total` sube en Grafana.
3. En el tablero técnico, los paneles de Redis muestran aciertos, fallos y porcentaje por `caso_de_uso`.
