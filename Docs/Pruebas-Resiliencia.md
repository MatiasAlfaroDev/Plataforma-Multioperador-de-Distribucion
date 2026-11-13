# Pruebas de resiliencia (opcional 7.4)

Cada prueba corta una dependencia con tráfico en curso y vuelve a levantarla. El script mide la API antes,
durante y después del corte:

```bash
scripts/prueba-resiliencia.sh redis|rabbitmq|postgres|receptor     # SEGUNDOS=30 por defecto
```

El tráfico es `GET /ready` (si la réplica se considera sana) y `GET /api/seguimiento/<token inventado>`, que
pasa por la caché y por la base y responde 404 sin necesitar sesión. Para RabbitMQ el script además cuenta las
filas del outbox. Junto a cada corrida conviene mirar el tablero técnico de Grafana y los logs (Loki).

## Comportamiento esperado

| Dependencia | Cómo se corta | Esperado durante el corte | Esperado al volver | De dónde sale |
| :-- | :-- | :-- | :-- | :-- |
| **Redis** (caché) | `docker compose stop redis` | La API sigue respondiendo: `ready=200`, `seguimiento=404`. Cada consulta a la caché cuenta como fallo y va a la base; sube `cache_errores_total`. La limitación de tasa compartida deja pasar. Las webs no pueden validar cookies nuevas (las claves de protección de datos están en Redis) y el tablero en vivo pierde el backplane. | Reconexión sola (`AbortOnConnectFail=false`); la caché vuelve a acertar. | ADR-005, `RedisCacheService`, `CacheSinRedisTests` |
| **RabbitMQ** (cola) | `docker compose stop rabbitmq` | La API sigue aceptando escrituras: `ready=200` (el bus no está en `/ready`). Cada cambio de estado deja su fila en `OutboxMessage`, que **crece**. El Worker no procesa: no salen avisos ni correos y el tablero en vivo no se actualiza solo. | El outbox se vacía y los avisos pendientes llegan al receptor, sin duplicados (idempotencia por suscripción y evento). | ADR-007, RNF 6.8 |
| **PostgreSQL** (base) | `docker compose stop postgres` | `ready=503` en las dos réplicas (el chequeo de base es parte de `/ready`) y `seguimiento=500`. Traefik saca las réplicas del balanceo. El Worker reintenta sus mensajes con espera exponencial. | `ready=200` en segundos; los mensajes que fallaron se reintentan; si agotaron los reintentos quedan en `*_error` y se reintentan desde Mensajes fallidos. | `BaseDeDatosHealthCheck`, `ConsumerDefinition` de cada consumidor |
| **Sistema del comercio** (servicio externo) | `POST receptor.localhost:8000/modo/falla` | La API no se entera. Los avisos fallan, el Worker reintenta con espera exponencial y sube `avisos_fallidos_total`; los que agotan los reintentos quedan como fallidos y en `*_error`. | Con `modo/ok`, los reintentos pendientes se entregan; los fallidos se reenvían desde el Backoffice (Avisos) o los toma la tarea `ReintentoDeAvisos`. | RNF 6.9, `EntregarAvisoConsumer`, `ReintentoDeAvisos` |

## Comportamiento observado

Completar con la salida del script (los conteos por fase) y una captura del panel que corresponda.

| Fecha | Dependencia | Antes | Durante | Después | ¿Coincide con lo esperado? | Notas |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| | Redis | | | | | |
| | RabbitMQ | | | | | |
| | PostgreSQL | | | | | |
| | Sistema del comercio | | | | | |

La caída de una réplica de la API (RNF 6.13) tiene su propio script: `scripts/prueba-caida-instancia.sh`.
