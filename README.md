# Plataforma Multioperador de Distribución de Última Milla

Plataforma SaaS multioperador para la gestión de distribución de última milla. Un único
despliegue atiende a varios operadores logísticos, cada uno con su marca, zonas, tarifas,
reglas y usuarios, sin acceso a los datos de los demás.

Laboratorio de Taller de Sistemas de Información .NET — UTEC, Tecnólogo en Informática, 2026.

## Aplicaciones

| Aplicación | Tecnología |
|---|---|
| Backoffice del operador | ASP.NET Core Razor Pages |
| Portal del comercio | Blazor |
| Seguimiento público | Blazor (sin autenticación) |
| Aplicación del repartidor | .NET MAUI (operación sin conectividad) |
| Procesamiento en segundo plano | Worker .NET |

Monolito modular en .NET 10 para API y web, más el worker desplegado de forma independiente
que se comunica solo de manera asíncrona a través de la cola de mensajes.

## Documentación

- [Letra del laboratorio](Docs/Letra-Laboratorio.md) — requerimientos y cronograma
- [Modelado](Docs/Modelado/) — arquitectura, despliegue, modelo de dominio, stack y entorno
- [Decisiones](Docs/Decisiones/) — máquina de estados, multitenancy y ADRs
- [Planificación](Docs/Planificacion/) — plan de trabajo, asignaciones y sprints

## Ejecución local

Pendiente. El entorno completo se levantará con `docker compose up`.
