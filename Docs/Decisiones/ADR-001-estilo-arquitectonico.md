# ADR-001 — Estilo arquitectónico y organización del código

**Responsable:** Juliana · **Estado:** aceptada · **Fecha:** 4/10/2026
**Revisado:** 7/10/2026 (se actualizó el árbol de carpetas al estado real del repositorio)

## Contexto y restricciones

La letra impone un monolito modular para la API y las aplicaciones web, más un Worker
independiente que se comunica solo por la cola. Exige que la lógica de dominio no dependa de
la infraestructura, del acceso a datos ni de los frameworks de presentación, y que esa regla
esté respaldada por al menos una prueba automatizada que corra en el pipeline (RNF 6.1).

El 29/10 llega un cambio de requerimientos no anunciado, de incorporación obligatoria, y
cada integrante debe poder explicar y defender su parte (§9.4). El equipo es de cuatro
personas y unas 430 horas en total.

## Decisión

**Clean Architecture con organización Vertical Slice**, el mismo esquema de la demo Support
vista en clase, extendido a un monolito modular.

Hay un proyecto por capa (Domain, Application, Infrastructure) compartido por todos los
módulos. Dentro de Domain y Application cada módulo de negocio es una carpeta. Dentro de
Application cada caso de uso es un *slice* con su Command o Query, su Handler y, si
corresponde, su Validator de FluentValidation.

Application accede a los datos solo mediante `IUnitOfWork` y repositorios declarados como
interfaces, que Infrastructure implementa con EF Core. La API y el Worker invocan Handlers;
las aplicaciones web consumen la API por HTTP y **no** referencian Application ni
Infrastructure. La regla de dependencias `Domain ← Application ← Infrastructure` se verifica
con NetArchTest en el pipeline.

### Árbol de carpetas

Estado real al 7/10/2026:

```
src/
  UltimaMilla.Domain/            No referencia ningún proyecto ni paquete.
    Common/        AuditableEntity, ITenantEntity
    Organizacion/  Operador, Comercio, CuentaComercial
    Envios/        Envio, Bulto, Destinatario, Direccion, EstadoEnvio, Modalidad, NuevoBulto
  UltimaMilla.Application/       Depende solo de Domain (+ FluentValidation).
    Abstractions/  IUnitOfWork, IEnvioRepository, ICuentaComercialRepository, IOperadorRepository
    Common/        Excepciones (NoEncontradoException, ConflictoException)
    Envios/        EnvioDto · Commands/CrearEnvio/ · Queries/ListarEnvios/
    CuentasComerciales/  CuentaComercialDto · Queries/ListarCuentasComerciales/
    Operadores/    OperadorDto · Queries/ListarOperadores/
  UltimaMilla.Infrastructure/    EF Core + PostgreSQL.
    Persistence/   AppDbContext, Configurations, Interceptors, Seed
    Repositories/  Implementaciones de los contratos + UnitOfWork
  UltimaMilla.Api/               Minimal API, ProblemDetails, /health /ready /live
  UltimaMilla.Web.Portal/        Blazor InteractiveServer
  UltimaMilla.Web.Backoffice/    ASP.NET Core Razor Pages
  UltimaMilla.Mobile/            .NET MAUI (MVVM) · solución aparte, fuera del CI
tests/
  UltimaMilla.UnitTests/           Reglas de dominio y del validador
  UltimaMilla.Architecture.Tests/  NetArchTest: la regla de dependencias
```

**Nombres que difieren del plan original.** `Organizacion/` es el módulo que el diagrama de
arquitectura lógica llama "Tenancy e Identidad": contiene el tenant (`Operador`) y el eslabón
con el comercio (`CuentaComercial`, ver ADR-002). Las pruebas de dominio viven en
`UltimaMilla.UnitTests` y no en un proyecto `Domain.Tests` separado; el proyecto se divide
cuando entren las de integración.

**Previsto, con el monitoreo en que entra.** `UltimaMilla.Contracts/` (contratos de la API
compartidos con las webs y la app móvil) y `Api/Endpoints/` (un archivo de mapeo por módulo);
`Domain/Envios/MaquinaEstadosEnvio` y `Persistence/Migrations/` el 15/10;
`Domain/Planificacion/` el 22/10; `Domain/Configuracion/`, `Infrastructure/Messaging/` y
`UltimaMilla.Worker/` el 29/10; `UltimaMilla.Web.Seguimiento/` según cronograma.

## Alternativas consideradas

- **Un juego de proyectos Domain, Application e Infrastructure por cada módulo.** Da límites
  más fuertes, porque el compilador impide que un módulo use el interior de otro. Descartada:
  serían más de 24 proyectos, excesivo para cuatro personas y nueve semanas. La separación por
  carpetas más la prueba de arquitectura por namespace alcanza.
- **Capas organizadas por tipo técnico, sin Vertical Slice** (todos los Handlers juntos, todos
  los Validators juntos). Descartada: un cambio en un caso de uso obliga a tocar archivos
  dispersos, y eso complica absorber el cambio de requerimientos del 29/10.
- **Las webs invocan los Handlers directamente**, como las Razor Pages de la demo.
  Descartada: hay cuatro clientes (Backoffice, Portal, Seguimiento y la app móvil) más la API
  pública para comercios. Si todos pasan por la API, la autorización y el aislamiento entre
  operadores se controlan en un único lugar.
- **Microservicios.** Descartada: la letra pide monolito modular, y multiplicaría despliegues,
  bases y fallas de red para un equipo de cuatro.

## Consecuencias

- A favor: es el mismo esquema de la demo de clase, así que todo el equipo parte de un modelo
  conocido. Agregar un caso de uso es agregar una carpeta, sin tocar los demás, lo que debería
  contener el cambio del 29/10 en pocos slices.
- En contra: los límites entre módulos dependen de la disciplina del equipo y de la prueba de
  arquitectura, no del compilador. Cada caso de uso lleva más archivos (Command, Handler,
  Validator, DTO).
- Que las webs pasen por la API agrega una llamada HTTP respecto de la demo; a cambio, hay una
  única puerta de autorización.
- No hay MediatR: los Handlers se registran a mano en `Application/DependencyInjection.cs`.
  Un slice nuevo sin ese registro compila y falla en tiempo de ejecución.
- Lo que dificulta a futuro: separar un módulo como servicio independiente (por ejemplo, el
  opcional del Seguimiento) exige mover su carpeta a proyectos propios.
