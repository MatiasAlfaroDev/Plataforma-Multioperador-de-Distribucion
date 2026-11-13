# ADR-007 — Consistencia entre la persistencia y la publicación de eventos

**Responsable:** Matías · **Estado:** aceptada · **Fecha:** 13/11/2026

## Contexto y restricciones

Cada cambio de estado de un envío tiene dos efectos: se guarda en PostgreSQL y se publica un evento en
RabbitMQ, que el Worker convierte en avisos al comercio (RF 28, RNF 6.9), en el correo con el enlace de
seguimiento, en la invalidación del seguimiento en caché (ADR-005) y en el aviso al tablero en vivo
(RF 29). La letra (RNF 6.8) exige que esos dos efectos no se separen:

- si el guardado falla, el evento no puede salir (un aviso de "entregado" para un envío que sigue en tránsito);
- si el guardado se confirma, el evento tiene que salir aunque RabbitMQ esté caído en ese momento;
- la API corre en dos réplicas y ninguna puede publicar dos veces el mismo mensaje ni pisar a la otra.

La base y el broker no comparten una transacción, así que no alcanza con publicar "justo después" de guardar.

## Decisión

**Outbox transaccional con el de MassTransit sobre EF Core** (`AddEntityFrameworkOutbox<AppDbContext>` con
`UsePostgres()` y `UseBusOutbox()`, en `Infrastructure/DependencyInjection.cs`).

- **Publicar es escribir una fila.** El `IPublishEndpoint` del scope no habla con RabbitMQ: agrega la fila
  `OutboxMessage` al mismo `AppDbContext`. Por eso el Handler publica con `IPublicadorDeEventos` **antes** de
  `GuardarCambiosAsync`, y el `SaveChanges` confirma el cambio y el mensaje en una única transacción de PostgreSQL.
- **Nadie se olvida de publicar los cambios de estado.** Ningún Handler publica `EstadoEnvioCambiado`: cada
  `Envio.Aplicar` deja un `EventoEnvio` y `EventosDeEstadoInterceptor` publica uno por cada evento nuevo
  dentro del mismo `SaveChangesAsync` (lo mismo con `EstadoBultoCambiado`). El interceptor rechaza el
  `SaveChanges` sincrónico si hay eventos, porque publicar en el outbox es asíncrono.
- **Entrega.** Un servicio en segundo plano de cada réplica lee el outbox y entrega a RabbitMQ. `UsePostgres()`
  toma las filas con bloqueo por fila (`FOR UPDATE SKIP LOCKED`), así las dos réplicas no entregan el mismo
  mensaje. Si RabbitMQ no está, los mensajes esperan en la tabla y salen cuando vuelve; por eso el bus no
  está en `/ready` (la API sigue aceptando escrituras).
- **Al menos una vez, consumidores idempotentes.** Entre la entrega y la confirmación puede repetirse un
  mensaje. Los consumidores lo toleran: cada `EstadoEnvioCambiado` trae su `EventoId`, el aviso al comercio
  se registra una vez por suscripción y clave de evento (`RegistrarAvisosHandler` consulta `ExisteAsync`), e invalidar caché o refrescar el tablero dos veces no cambia nada.
- **Lo que falla después de publicar** lo cubren los reintentos exponenciales de cada `ConsumerDefinition`
  y la cola `*_error`, que el Backoffice permite reintentar (Mensajes fallidos).
- **Trazabilidad.** El mensaje guarda sus encabezados en el outbox, así el `traceparent` y el
  `X-Correlation-Id` del pedido original llegan al Worker aunque la entrega ocurra minutos después (RNF 6.12).

## Alternativas consideradas

| Alternativa | Por qué se descartó |
| :-- | :-- |
| Publicar en RabbitMQ después del `SaveChanges` | Si el proceso cae o RabbitMQ no responde entre las dos cosas, el cambio queda guardado y el evento se pierde. Es exactamente lo que prohíbe el RNF 6.8. |
| Publicar antes de guardar | Lo contrario: sale un aviso de un cambio que después falla al guardarse. |
| Transacción distribuida (2PC) | RabbitMQ no participa en transacciones XA y Npgsql no las soporta en .NET moderno. Además acopla la disponibilidad de la API a la del broker. |
| Captura de cambios (Debezium leyendo el WAL) | Garantía equivalente sin tocar el código, pero suma Kafka Connect y un conector que hay que operar. Desproporcionado para un monolito con una base. |
| Outbox propio (tabla y job) | Es lo mismo que trae MassTransit, sin bloqueo entre réplicas, deduplicación ni métricas. MassTransit 8 es Apache-2.0 y ya está en el stack. |

## Consecuencias

- El evento sale con un pequeño retraso (lo que tarda el servicio de entrega en leer la tabla). Para avisos y
  tablero es imperceptible.
- Toda escritura que publique tiene que pasar por `SaveChangesAsync` del `AppDbContext`: un `ExecuteUpdate`
  directo o el `SaveChanges` sincrónico no dejarían el mensaje.
- Los consumidores **tienen** que ser idempotentes; un consumidor nuevo que no lo sea duplicaría efectos.
- Mover un evento de namespace rompe a sus consumidores: MassTransit nombra el exchange por tipo.
- La versión 9 de MassTransit cambia de licencia; quedar en 8.x limita las actualizaciones futuras.

## Verificación

1. `docker compose stop rabbitmq`, cambiar el estado de un envío desde el Backoffice: la API responde bien y
   la fila queda en `OutboxMessage`. `docker compose start rabbitmq`: el aviso llega al receptor y la tabla se vacía.
2. `UnitOfWorkRollbackTests` (pruebas de integración): un error a mitad de camino revierte toda la transacción,
   y las filas del outbox viajan en esa misma transacción.
3. En Grafana, el panel "Entregas del outbox por evento" muestra lo entregado, y el `X-Correlation-Id` del
   pedido aparece en los logs del Worker que lo procesó.
