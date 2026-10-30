# ADR-003 — Política de resolución de conflictos de sincronización

**Responsable:** Cecilia · **Estado:** aceptada · **Fecha:** 27/10/2026

## Contexto y restricciones

El repartidor trabaja en la calle y pierde la señal con frecuencia: la letra describe sectores del
área metropolitana y buena parte del interior donde trabaja horas sin conexión utilizable. Aun así,
tiene que poder registrar cada entrega o intento fallido **en el momento**, porque si espera a tener
señal puede olvidarse o perder la evidencia. Y el servidor tiene que terminar guardando el historial
de hechos **completo y en orden**, con la hora real en que ocurrieron.

Mientras el teléfono está desconectado, el servidor sigue cambiando: el despachador puede quitar un
envío de la ruta, otro repartidor puede registrar algo sobre el mismo envío, o las reglas del operador
pueden exigir datos que el teléfono no mandó. Al volver la señal, lo que dice el teléfono y lo que
dice el servidor pueden ser **incompatibles**. Eso es un conflicto.

La letra exige (RF 24 y RNF 6.11) operación completa sin conectividad, almacenamiento local cifrado y
sincronización diferida con una **política explícita de resolución de conflictos**, y en la defensa
se va a "provocar un conflicto de sincronización y observar la aplicación de la política" (9.3,
escenario 3). Restricciones del sistema:

- El estado de cada envío lo controla la **máquina de estados** del servidor (RF 11, ADR de Juliana):
  `Entregar` y `RegistrarIntentoFallido` solo se aplican desde *En tránsito* y con las reglas
  operativas del operador (prueba de entrega, catálogo de motivos, máximo de intentos).
- El servidor atiende a todos los repartidores de todos los operadores (ADR-002) y corre en **dos
  instancias**: un reintento puede caer en cualquiera de las dos.
- El teléfono guarda sus datos en una base **SQLite cifrada con SQLCipher**, con la clave en el
  SecureStorage del sistema operativo.

## Decisión

**El teléfono registra hechos; el servidor decide si son válidos y es la única fuente de verdad. Lo
que el servidor rechaza no se pierde ni se aplica: queda visible en el teléfono hasta que el
repartidor lo descarta.**

1. **Primero en el teléfono, siempre.** Al tocar *Entregado* o *No entregado*, la app guarda el
   hecho en la cola local `eventos_pendientes` y marca el envío, en una sola transacción, haya o no
   señal. Recién después intenta mandarlo.
2. **Hechos, no estados.** El teléfono no dice "el envío está Entregado": dice "a las 14:03 se
   entregó a Ana Pérez, con esta foto y en esta posición". El servidor aplica la transición.
3. **Idempotencia por un Id del teléfono.** Cada hecho lleva un `Guid` generado en el teléfono al
   registrarlo. En el servidor es la **clave primaria** de `eventos_movil`. Si llega dos veces, el
   servidor responde **409** sin volver a aplicarlo, y la app lo da por confirmado.
4. **Sincronización en orden y sin pérdidas.** El `SincronizacionService` manda la cola ordenada por
   la hora del hecho, se dispara solo al volver la conexión (`ConnectivityChanged`) o con el botón
   *Enviar ahora*, nunca corre dos veces a la vez, y **solo borra lo que el servidor confirmó**. Si
   se corta la señal, se detiene y sigue después.
5. **Gana el servidor.** El Handler `RegistrarEventoMovilHandler` valida cada hecho contra el estado
   actual del envío y las reglas del operador. Si es incompatible, responde **400** con el motivo
   (en el campo `title` del ProblemDetails) y no aplica nada.
6. **El rechazo queda visible.** La app guarda el motivo en la cola (`ultimo_error`) y la tarjeta del
   envío pasa a **"Rechazado"** en rojo, con el motivo y un botón **Descartar**. Un rechazado no
   cuenta como entregado ni como no entregado.
7. **El repartidor cierra el conflicto.** Al descartar, se borra lo registrado en el teléfono y el
   envío vuelve a *Por entregar*. Así el teléfono adopta lo que dice el servidor y, si corresponde,
   se puede volver a registrar con los datos correctos.
8. **La hora real del hecho.** Se guardan dos horas: `OcurridoEn` (la del teléfono, en la calle) y
   `RecibidoEn` (la del servidor). Se admite una tolerancia de 5 minutos para relojes adelantados, y
   un hecho no puede ser anterior al alta del envío.
9. **Una descarga nunca pisa lo pendiente.** Al bajar la ruta otra vez, lo que el repartidor registró
   y no se confirmó se conserva, y solo se guardan envíos del operador de la sesión.

### Casos de conflicto y cómo se resuelven

| Caso | Ejemplo | Respuesta del servidor | Qué ve el repartidor |
| :---- | :---- | :---- | :---- |
| Reintento del mismo hecho | Se cortó la señal después de que el servidor guardó, pero antes de recibir la respuesta | **409**: ya lo tenía, no se duplica | Nada raro: queda "✓ Enviado al servidor" |
| Dos entregas del mismo envío | Dos teléfonos registran la entrega del mismo paquete | **400**: "Conflicto: el envío ya figura como entregado a …" | La segunda queda "Rechazado" con ese motivo |
| Estado incompatible | El envío no fue despachado, o lo quitaron de la ruta mientras el repartidor estaba sin señal | **400**: "El envío debe estar en tránsito …" | "Rechazado" con el motivo |
| Hecho que no cumple las reglas del operador | Entrega sin foto ni firma, sin GPS, motivo que no está en el catálogo, máximo de intentos alcanzado | **400 / 404** con el motivo | "Rechazado" con el motivo |
| Envío inexistente o de otro operador | Datos de una ruta vieja, o de otro inquilino | **404**: "El envío no existe" | "Rechazado" con el motivo |
| Sesión vencida | La cookie de sesión expiró | **401** | "Rechazado: Sesión vencida", que se resuelve iniciando sesión de nuevo |
| Sin conexión | Modo avión, API caída | Sin respuesta | "⏳ Guardado en el teléfono · falta enviarlo"; se reintenta solo |

## Alternativas consideradas

| Alternativa | Por qué se descartó |
| :---- | :---- |
| **El último que sincroniza gana** (*last write wins*) | Un teléfono que estuvo horas sin señal pisaría lo que ya decidió el servidor, por ejemplo una reasignación. El resultado depende de quién tuvo señal primero, no de lo que pasó |
| **El teléfono siempre gana** | El servidor dejaría de controlar la máquina de estados y las reglas del operador: se podría entregar un envío nunca despachado o sin prueba de entrega. Con varios repartidores, dos teléfonos podrían dejar el mismo envío en estados distintos |
| **No dejar trabajar sin señal** | Incumple el RF 24. Además el repartidor tendría que esperar a tener señal para registrar, y los hechos se perderían o quedarían con una hora falsa |
| **Rechazar en silencio** (el servidor rechaza y la app borra) | Se pierden datos sin que nadie se entere: una entrega real podría desaparecer y no quedaría evidencia para un reclamo |
| **Reservar el envío en el teléfono** (bloqueo pesimista) | Exige conexión para tomar la reserva, que es justo lo que falta en la calle, y deja envíos bloqueados si el teléfono se apaga o se pierde |
| **Fusionar automáticamente** | Entre "entregado" y "no entregado" no hay una mezcla razonable: alguien tiene que decidir cuál vale. Una regla automática escondería el conflicto |

## Consecuencias

**A favor:**
- **Nunca se pierde un registro:** se guarda primero en el teléfono, se borra solo lo confirmado, y
  lo rechazado queda visible.
- **Nunca se aplica dos veces lo mismo:** la idempotencia la garantiza la clave primaria, aunque dos
  reintentos lleguen a la vez a instancias distintas de la API.
- **El estado de un envío es uno solo para todos**, porque lo decide el servidor con sus reglas.
- **El historial queda con la hora real** de cada hecho y la hora en que llegó.

**En contra (lo que esta decisión dificulta):**
- **El repartidor tiene que intervenir a mano.** Un "Rechazado" no se resuelve solo: tiene que leer
  el motivo y descartarlo. Si no lo hace, queda en la cola.
- **Cuanto más tiempo sin señal, más conflictos.** Si pasan horas, es más probable que el despachador
  haya cambiado algo. El conflicto se resuelve, pero el trabajo hecho en la calle se puede perder
  para el sistema (no para el teléfono hasta descartarlo).
- **Los rechazados se reintentan en cada sincronización** y vuelven a ser rechazados, hasta que el
  repartidor los descarta. El contador `intentos` lo registra.
- **La política vive en dos lugares.** Las reglas están en el servidor (Handler y máquina de estados)
  y la presentación del conflicto en la app: un cambio de reglas no requiere actualizar la app, pero
  un caso nuevo puede requerir mostrarlo distinto.
- **Pendiente:** la foto viaja como referencia y el archivo queda en el teléfono hasta implementar la
  subida; la sesión no sobrevive al cierre de la app, así que una sincronización tras reabrirla
  necesita iniciar sesión otra vez.

## Verificación

- Pruebas unitarias del Handler (`RegistrarEventoMovilHandlerTests`): el mismo evento dos veces no
  duplica nada; una segunda entrega con otro evento se rechaza; un envío inexistente da no
  encontrado; y se rechazan la entrega sin coordenadas, la entrega sin firma ni foto cuando las
  reglas lo exigen, el intento fallido sin evidencia y el motivo ajeno al catálogo. **Falta** una
  prueba para el envío que no está en tránsito.
- **Demostración del escenario 3:** registrar *Entregado* en un envío **sin despachar**, con la app
  sin conexión; recuperar la conexión y sincronizar; la tarjeta muestra **"Rechazado: El envío debe
  estar en tránsito…"** y el botón **Descartar**; al descartar, el envío vuelve a *Por entregar*.
  Variante con dos dispositivos: entregar el mismo envío desde los dos; el segundo queda
  "Rechazado: el envío ya figura como entregado a …".
