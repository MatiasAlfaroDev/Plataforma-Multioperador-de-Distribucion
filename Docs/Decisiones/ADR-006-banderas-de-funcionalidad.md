# ADR-006 — Banderas de funcionalidad por operador

**Estado:** aceptada · **Fecha:** 12/11/2026

## Contexto

La aclaración 3.2 distingue la configuración (reglas y tarifas, que **ajustan** algo que existe) de las
banderas, que deciden **si una funcionalidad está disponible** para un operador. Las banderas las activa
el administrador de la plataforma, no los usuarios del operador. Para otorgar el punto se verifica que:

- haya al menos dos funcionalidades reales bajo bandera, activables por operador desde una interfaz;
- la bandera se respete en la API, las webs, el Worker y la app del repartidor;
- un cambio llegue a todas las réplicas en un tiempo acotado y conocido, sin reiniciarlas;
- quede registro de quién la modificó y cuándo.

## Decisión

**Implementación propia, chica**, en lugar de `Microsoft.FeatureManagement`.

- **Modelo.** `BanderaFuncionalidad` es una fila por (bandera, operador) y `CambioBandera` es el historial.
  No tienen tenant: son de la plataforma. Si no hay fila, la funcionalidad está habilitada, que es como
  funcionaba antes de las banderas.
- **Funcionalidades bajo bandera** (las dos del cambio de requerimientos):
  - `entrega-parcial`
  - `retiro-domicilio`
- **Lectura.** `IBanderas.EstaHabilitadaAsync(bandera, operador)` guarda en Redis la lista de banderas
  apagadas del operador durante 15 s y, si Redis no está, va a la base.
- **Propagación.** El cambio borra la entrada de Redis, así que la próxima lectura de cualquier réplica ya
  ve el valor nuevo. Si el borrado fallara, **el máximo es 15 s**. Nada se reinicia.
- **Auditoría.** Cada cambio guarda quién (el usuario de la sesión) y cuándo; el Backoffice lo muestra.
- **Interfaz.** Página *Banderas de funcionalidad* del Backoffice, solo para `AdministradorPlataforma`
  (`GET/PUT /api/admin/banderas`).

Dónde se respeta cada bandera:

| Capa | `entrega-parcial` | `retiro-domicilio` |
| :-- | :-- | :-- |
| API | `RegistrarEventoMovilHandler` rechaza la entrega parcial. | `CrearRetiroHandler` responde 409. |
| Worker | `GenerarAvisosConsumer` no genera avisos por bulto. | — |
| Webs | — | El Portal no ofrece retiros con ese operador; el Backoffice no muestra paradas de retiro al armar hojas. Las dos leen `GET /api/banderas`. |
| App | `GET /api/motivos-no-entrega` informa si se admite. Si no, la app no ofrece elegir bultos. | No le llegan paradas de retiro, porque no se pueden crear. |

## Alternativas consideradas

| Alternativa | Por qué se descartó |
| :-- | :-- |
| `Microsoft.FeatureManagement` con un filtro por inquilino | Resuelve la evaluación, pero no la persistencia, la interfaz ni la auditoría, que había que hacer igual. El filtro sería la misma consulta por (bandera, operador) con una capa más. |
| Banderas en `appsettings` | Cambiarlas requiere redesplegar o recargar cada instancia, y no se configuran por operador desde una interfaz. |
| Que lo decida el operador en sus reglas | Contradice la aclaración: es el plan que ofrece la plataforma, no una regla del operador. |

## Consecuencias

- Agregar una bandera es una constante en `Banderas` más los puntos donde se consulta.
- Lo que ya estaba en curso cuando se apaga una bandera, por ejemplo un retiro asignado, se completa.
  Solo se impide empezar algo nuevo.
