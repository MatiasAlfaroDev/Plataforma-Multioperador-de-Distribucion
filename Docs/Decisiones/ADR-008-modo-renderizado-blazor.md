# ADR-008 — Modo de renderizado de Blazor

**Responsable:** Martina · **Estado:** aceptada · **Fecha:** 13/11/2026

## Contexto y restricciones

La letra pide el Portal del comercio y el seguimiento público en Blazor, y deja el modo de renderizado a
elección del equipo (sección 6.1 del informe). Las restricciones que pesan:

- **Las webs no tocan la base ni Application.** Portal y Backoffice hablan con la API por HTTP (ADR-001); el
  Portal no puede tener reglas de negocio propias.
- **Sesión con cookie.** La API emite `UltimaMilla.Auth` (HttpOnly), protegida con claves compartidas en Redis
  entre API, Portal y Backoffice. No hay tokens que guardar en el navegador.
- **Más de una instancia** (RNF 6.13) con protección de datos compartida, cuando se usan cookies o Blazor Server.
- **Seguimiento público** sin sesión y protegido contra la enumeración (RNF 6.3): el token no debería salir del
  servidor más de lo necesario.
- Equipo de cuatro, con un solo lenguaje (C#) para la interfaz y el servidor.

## Decisión

**Blazor Web App con renderizado interactivo en el servidor (`InteractiveServer`) para todo el Portal**:
`AddInteractiveServerComponents()` en `Web.Portal/Program.cs` y `@rendermode="InteractiveServer"` en
`Routes` y `HeadOutlet` (`Components/App.razor`). El Backoffice queda en Razor Pages (no es Blazor).

- **El código de la interfaz corre en el servidor.** Las consultas sin sesión, como el seguimiento, las hace el
  Portal con sus `HttpClient` tipados (`SeguimientoApiClient`, `EnviosApiClient`). Las que dependen de la
  cookie del comercio (login, alta de usuarios, retiros) las hace el navegador con pequeños módulos de
  `wwwroot/js` contra `/api` del **mismo origen**, que Traefik deriva a la API: la cookie viaja sola y no hace
  falta CORS.
- **Seguimiento público:** la consulta con el token la hace el servidor del Portal; el límite por visitante se
  aplica en el Portal (`AddSeguridadWebPortal`) y otro, más holgado, en la API.
- **Varias instancias:** las claves de Data Protection están en Redis, y Traefik mantiene cada circuito en su
  réplica con la cookie de afinidad `UltimaMilla.Afinidad` (compose). Blazor Server necesita afinidad, no un
  backplane: el circuito vive en una sola instancia.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
| :-- | :-- |
| `InteractiveWebAssembly` | Descargar el runtime hace lenta la primera carga del seguimiento, que se abre desde un correo en el celular, y todo el código y los contratos de la interfaz quedan en el navegador. El estado de autenticación habría que reconstruirlo del lado del cliente. |
| `InteractiveAuto` | Suma los dos modelos: los componentes tienen que funcionar en servidor y en navegador, con dos formas de llamar a la API. Complejidad que no paga en un laboratorio. |
| Renderizado estático (SSR) con formularios | Sin interactividad (validación en vivo, listas que se actualizan, importar CSV con progreso); habría que agregar JavaScript a mano en cada pantalla. |
| Razor Pages también para el Portal | Mismo argumento que el SSR estático; la letra pide Blazor para el Portal. Se mantiene para el Backoffice, donde las pantallas son formularios y tablas. |

## Consecuencias

- Cada usuario conectado tiene un circuito en memoria del servidor y una conexión SignalR abierta: la memoria
  crece con los usuarios simultáneos, y el Portal escala agregando réplicas con afinidad.
- Si se cae la réplica que tiene el circuito, el usuario ve el aviso de reconexión y el estado de la pantalla
  se pierde (la sesión no, porque es la cookie). Es aceptable para formularios cortos.
- Toda interacción viaja al servidor: con mala señal la interfaz se siente lenta. El repartidor no usa el
  Portal (tiene la app), así que el impacto es menor.
- Conviven dos formas de llamar a la API (desde el servidor del Portal y desde el navegador por el mismo
  origen); una pantalla nueva tiene que elegir según necesite o no la cookie del comercio.

## Verificación

1. `portal.localhost:8000`: en las herramientas del navegador hay una conexión `/_blazor` (WebSocket) y ninguna
   descarga de `dotnet.wasm`.
2. Al consultar un seguimiento, la llamada a la API sale del contenedor del Portal (traza Portal → API en Grafana
   con un mismo traceId).
3. Con la cookie `UltimaMilla.Afinidad` borrada, la página se recarga y el circuito se crea de nuevo sin perder la sesión.
