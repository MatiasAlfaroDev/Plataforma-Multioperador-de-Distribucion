# ADR-004 — Incorporación del cambio de requerimientos (entrega parcial y retiro en domicilio)

**Estado:** aceptada · **Fecha:** 12/11/2026

## Contexto

El 30/10 se presentó el cambio de requerimientos (`Docs/Cambio-Requerimientos.md`). El equipo es de
cuatro integrantes, así que incorpora los dos cambios:

- **2.1 Entrega parcial de bultos (RF 31 a 36):** cada bulto tiene su propio resultado y el estado del
  envío se deriva de los bultos.
- **2.2 Retiro en domicilio (RF 37 a 41):** un flujo que empieza en el comprador y termina en el comercio,
  con hojas de ruta mixtas.

Ambos tienen que respetar las mismas exigencias no funcionales: máquina de estados declarativa,
configuración versionada por operador, outbox, idempotencia en el consumidor, operación sin conexión y
aislamiento entre inquilinos. La sección 2.3 pide que este registro analice qué componentes se
modificaron, qué resultó sencillo y qué no.

## Decisión

### Entrega parcial

| RF | Cómo se resolvió | Componentes |
| :-- | :-- | :-- |
| 31 | `Bulto` tiene su propia máquina Stateless (`MaquinaEstadosBulto`). El resultado de cada visita se **deriva** de los bultos con `MaquinaEstadosBulto.ResultadoDeVisita(pendientes, entregados)`: todos entregados → *Entregado*, algunos → *EntregadoParcialmente*, ninguno → *NoEntregado*. Cada transición del envío se traslada a sus bultos (también extravío y reaparición) y deja un `EventoBulto`. | Domain (`Envio`, `Bulto`, `EventoBulto`), Infrastructure (tabla `eventos_bulto`) |
| 32 | La app escanea los bultos que entrega y elige el motivo de los demás del **catálogo del operador**, que descarga con la ruta (`GET /api/motivos-no-entrega`). La prueba de entrega queda en el `IntentoEntrega`, con un `ResultadoBultoIntento` por bulto. Los conflictos por bulto están en ADR-003. | Mobile, Api, Application |
| 33 | `VersionReglasOperativas` (ya versionada) agrega *admite entrega parcial*, *acción para los pendientes* (nueva visita o devolución) y *nueva visita con recargo*. Al volver al depósito, los pendientes se **reprograman** (con recargo opcional) o pasan a **devolución** sin intervención manual. | Domain, Application (`RetornarADepositoHandler`), Backoffice |
| 34 | Tras la reprogramación automática el envío queda *Reprogramado* y puede asignarse a otra hoja; la capacidad solo cuenta los bultos pendientes. | Domain |
| 35 | El seguimiento público agrega *bultos entregados / total*, sin identificadores. | Application, Portal |
| 36 | El contrato de los avisos se **versiona por suscripción**: la v1 queda idéntica (lo fija una prueba) y la v2 agrega `version`, el detalle de bultos y los tipos `bulto.*`, que el Worker genera desde `EstadoBultoCambiado`. Una suscripción v1 nunca recibe avisos de bulto. | Domain (`SuscripcionAviso`), Application, Worker, receptor de prueba |

### Retiro en domicilio

| RF | Cómo se resolvió | Componentes |
| :-- | :-- | :-- |
| 37 | El comercio lo solicita desde el Portal (página *Retiros*) o **por API**, con una clave por usuario del comercio en el encabezado `X-Api-Key`. Por defecto se entrega en la dirección de recepción de la cuenta. | Portal, Api (`ClaveApiAuthenticationHandler`) |
| 38 | `Retiro` tiene su propia máquina Stateless: solicitado, asignado a ruta, retirado, no retirado, en depósito, entregado al comercio. Un retiro no retirado se reprograma. | Domain, Application |
| 39 | `ParadaRuta` distingue entrega, devolución, retiro y entrega de retiro al comercio. `PlanificacionHojaDeRuta` simula la carga parada por parada: los bultos retirados ocupan lugar desde que se suben. | Domain, Backoffice |
| 40 | La app registra el retiro sin conexión con el escaneo de los bultos, una foto y la firma de quien entrega, en la misma cola que las entregas. | Mobile, Api |
| 41 | Tiene su tarifa (`TipoServicioTarifario.RetiroDomicilio`), usa el **mismo enlace público** que los envíos (`/seguimiento/{token}`) y está en avisos, en el tablero del Backoffice y en reportes. | Application, Api, Portal, Backoffice |

## Qué fue sencillo

- **Máquinas de estado declarativas.** Agregar *EntregadoParcialmente*, la máquina del bulto y la del
  retiro fue configurar transiciones nuevas en Stateless, sin tocar las que ya había.
- **Configuración versionada.** Las reglas de la entrega parcial son tres campos más en una versión que
  ya era inmutable y con vigencia: no hubo que diseñar el versionado.
- **Outbox e interceptor de eventos.** `EventosDeEstadoInterceptor` ya publicaba un evento por transición
  del envío. Publicar uno por cambio de bulto fue repetir el patrón, y los avisos v2 salieron de ahí sin
  que ningún handler tuviera que acordarse de publicarlos.
- **Aislamiento entre inquilinos.** `EventoBulto` y `Retiro` heredan el `OperadorId`; los filtros globales
  y el interceptor de guardado los cubrieron sin código nuevo.

## Qué no fue sencillo

- **Derivar el estado del envío de los bultos.** La máquina del envío estaba guiada por disparadores
  (la app decía "entregado"). Pasar a derivarlo obligó a definir un único lugar
  (`ResultadoDeVisita`) y a que cada transición del envío se refleje en los bultos, incluidos el extravío
  y la reaparición, que antes dejaban los bultos inconsistentes.
- **Versionar el contrato de los avisos.** El cuerpo se armaba en un solo lugar sin versión. Hubo que
  llevar la versión a la suscripción, armar el cuerpo por versión y blindar la v1 con una prueba que
  falla si se le agrega un campo.
- **Conflictos sin conexión por bulto.** Dos registros de la misma parada pueden chocar en un bulto y no
  en otro. Se mantuvo la política de ADR-003 (el servidor decide, el evento se acepta o se rechaza
  completo) y se mejoró el rechazo para que nombre los bultos en conflicto.
- **Hojas de ruta mixtas.** La validación de capacidad pasó de una suma a una simulación del recorrido,
  y la app y la carga del vehículo tuvieron que dejar de suponer que toda parada es un envío.

## Consecuencias

- El ciclo del envío ya no depende de lo que diga la app: un evento parcial con todos los bultos se
  registra como entrega completa.
- Los comercios que ya consumen avisos no ven cambios hasta que se suscriben a la v2.
- Las dos funcionalidades quedan detrás de banderas por operador (ADR-006).
