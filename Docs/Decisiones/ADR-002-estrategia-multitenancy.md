# ADR-002 — Estrategia de multitenancy

**Responsable:** Martina · **Estado:** aceptada · **Fecha:** 4/10/2026

## Contexto y restricciones

Un único despliegue atiende a varios operadores logísticos, que no deben ver datos entre sí.
Hay un segundo nivel de aislamiento: dentro de cada operador, un comercio solo ve lo suyo, y
un mismo comercio puede trabajar con varios operadores a la vez.

La letra exige (RNF 6.5) resolución del inquilino en tiempo de ejecución, aislamiento efectivo
en lecturas y escrituras con pruebas automatizadas que validen que no hay filtración,
migraciones y datos de inicialización con soporte multi-inquilino, identidad visual y reglas
de negocio diferenciadas por inquilino, y una demostración con al menos dos operadores de
configuración distinta. El ambiente debe caber en el nivel gratuito de la nube y reconstruirse
con un comando.

## Decisión

**Base de datos y esquema compartidos, con aislamiento por fila.** Toda entidad que pertenece
a un operador lleva `OperadorId` y las que pertenecen a un comercio llevan además
`ComercioId`. Las entidades con inquilino implementan `ITenantEntity`.

El `Comercio` es una identidad global, vinculada a cada operador mediante una
**`CuentaComercial`** (operador + comercio). El `Envio` cuelga de la cuenta y **hereda su
`OperadorId`**: `Envio.Crear` lo deriva de la cuenta y nunca lo acepta desde afuera, que es
hoy el único punto donde el inquilino se asigna.

El aislamiento se aplica con filtros globales de EF Core (`HasQueryFilter`) alimentados por un
`ITenantContext` con alcance por solicitud, más un interceptor de `SaveChanges` que asigna
`OperadorId` a toda entidad nueva y rechaza cualquier modificación cuyo `OperadorId` no
coincida con el contexto actual. El uso de `IgnoreQueryFilters` se restringe a un único
repositorio de administración de plataforma, verificado por prueba de arquitectura.

### Resolución del inquilino en tiempo de ejecución

| Canal | Cómo se resuelve |
| :---- | :---- |
| Backoffice, Portal, API autenticada | Claim `operador_id` del token; para usuarios de comercio, también `comercio_id` |
| App del repartidor | Claims del token del repartidor, validados en cada sincronización |
| Seguimiento público | El token opaco del enlace se busca en un índice y determina operador y envío |
| Worker | El `OperadorId` viaja dentro de cada mensaje y se fija en el contexto antes de procesarlo |
| SignalR | Al conectar, el usuario se une al grupo `operador:{id}`; nunca recibe mensajes de otro grupo |

### Estado de implementación al 7/10/2026

Implementado: `ITenantEntity`, `OperadorId` en `Envio` y `CuentaComercial`, la herencia del
operador desde la cuenta en `Envio.Crear`, índice compuesto `(OperadorId, FechaAlta)`, y los
datos de demostración con dos operadores (RapiEnvíos y Logística Sur) donde *Tienda Norte*
tiene cuenta con los dos.

**Pendiente para el 15/10, y por eso provisorio:** no hay login, así que el operador y la
cuenta se eligen en pantalla y `GET /api/cuentas-comerciales` existe solo para eso. Faltan el
`ITenantContext`, los filtros globales de consulta y el interceptor que asigne y valide el
`OperadorId`. Mientras tanto `GET /api/envios?operadorId=` filtra por un parámetro de query
opcional: omitirlo devuelve los envíos de todos los operadores. Es el agujero que cierra el
monitoreo del 15/10.

## Alternativas consideradas

Se evaluaron las cinco estrategias del teórico de Arquitectura, de más compartida a más
aislada:

| Estrategia | Ventajas | Desventajas | Evaluación |
| :---- | :---- | :---- | :---- |
| **Compartir tablas** (una base, un esquema, columna de inquilino) | Bajo costo, menos almacenamiento, una sola migración | Consultas más complejas; si una tabla se corrompe afecta a todos | **Elegida.** Los filtros globales de EF Core resuelven la complejidad de las consultas, y encaja con el comercio compartido entre operadores |
| Tablas distintas por cliente (una base) | Mejor aislamiento y personalización | Muchas más tablas, nombres distintos en cada consulta | Descartada: EF Core no está pensado para cambiar el nombre de las tablas por solicitud |
| Un esquema por cliente (una base) | Mejor aislamiento, las consultas son iguales | Más almacenamiento; cada migración se aplica N veces | Descartada: complejidad de migraciones, y el comercio compartido quedaría duplicado en varios esquemas |
| Una base por cliente (mismo motor) | Buen aislamiento, mantenimiento independiente | Más almacenamiento y memoria, límite de bases | Descartada: multiplica conexiones y costo; dar de alta un operador exigiría aprovisionar infraestructura |
| Un servidor por cliente | El mejor aislamiento | Más costos y licencias, límite de instancias | Descartada: excede el nivel gratuito de la nube y la escala de un proyecto de nueve semanas |

Se descartó además **aplicar el aislamiento a mano** (un `WHERE` en cada consulta dentro de la
opción elegida), porque depende de que nadie lo olvide y no se puede verificar de forma
centralizada.

Los desafíos que el teórico asocia a multi-tenancy se atienden así: **aislamiento** y
**seguridad** con filtros globales, interceptor de escrituras y pruebas automatizadas;
**customización** con marca, tarifario y reglas configurables por operador; **actualizaciones**
con una única migración para todos; y **recuperación** con respaldos de la base compartida,
aceptando que no se puede restaurar un solo operador por separado.

## Consecuencias

- A favor: una sola migración para todos; dar de alta un operador es insertar filas; las
  consultas de plataforma (métricas globales) son simples; el modelo de comercio compartido
  entre operadores encaja naturalmente.
- En contra: un error que omita el filtro puede filtrar datos. Mitigaciones: filtros globales
  en lugar de `WHERE` manuales, prohibición de `IgnoreQueryFilters` fuera de un repositorio
  autorizado (verificada por prueba de arquitectura), pruebas automatizadas de aislamiento por
  endpoint, y Row-Level Security de PostgreSQL como posible defensa adicional.
- Vecino ruidoso: un operador con mucho volumen afecta el rendimiento de los demás. Se mitiga
  con índices compuestos que empiezan por `OperadorId` y límites de tasa por inquilino.
- `ITenantEntity.OperadorId` es de solo lectura, así que el interceptor de escrituras no puede
  asignarlo sin una propiedad sombra, reflexión o un método propio en la interfaz. Hay que
  resolverlo al implementarlo.
- Lo que dificulta a futuro: si un operador exigiera su propia base por contrato, habría que
  migrar sus datos a una instancia aparte. El `OperadorId` en todas las tablas facilita esa
  extracción, pero no es inmediata.

## Pruebas automatizadas de aislamiento

Con Testcontainers se crean dos operadores con datos y, para cada endpoint de lectura, se
verifica que el operador A no obtiene datos de B, que un comercio no ve envíos de otro
comercio del mismo operador, y que una escritura con un id ajeno devuelve 404. Dependen de que
existan los filtros globales, así que entran con ellos el 15/10.
