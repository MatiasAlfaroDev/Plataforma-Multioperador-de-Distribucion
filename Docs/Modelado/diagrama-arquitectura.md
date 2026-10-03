# Arquitectura lógica

Una sola API autoriza; el Worker trabaja solo desde la cola.

```mermaid
flowchart TB
  MAUI["App repartidor<br/>.NET MAUI · offline"]
  BO["Backoffice<br/>Razor Pages"]
  POR["Portal del comercio<br/>Blazor Server"]
  SEG["Seguimiento público<br/>Blazor SSR · sin login"]

  TRF["Traefik<br/>TLS · balanceo · rate limit"]

  subgraph API["API · monolito modular · 2 instancias · REST + hub SignalR"]
    M1["Tenancy e Identidad"]
    M2["Configuración"]
    M3["Envíos"]
    M4["Planificación"]
    M5["Ejecución y Sincronización"]
    M6["Seguimiento"]
    M7["Avisos y Notificaciones"]
    M8["Operación y Reportes"]
    M1 ~~~ M5
    M2 ~~~ M6
    M3 ~~~ M7
    M4 ~~~ M8
  end

  PG[("PostgreSQL<br/>datos · outbox")]
  RD[("Redis<br/>caché · backplane · locks")]
  MQ[["RabbitMQ<br/>colas · reintentos · fallidos"]]

  W["Worker .NET<br/>proceso aparte · relay del outbox"]
  OTEL["OTel Collector<br/>Prometheus · Tempo · Grafana"]

  DEST(["Destinatarios<br/>correo o mensajería"])
  COM(["Sistemas del comercio<br/>webhooks firmados"])

  MAUI --> TRF
  BO --> TRF
  POR --> TRF
  SEG --> TRF
  TRF --> API

  API --> PG
  API --> RD
  API --> MQ
  MQ --> W
  W --> PG

  W -.-> DEST
  W -.-> COM
  API -.-> OTEL
  W -.-> OTEL

  classDef cliente fill:#e7eefb,stroke:#3f6fb5,stroke-width:1.5px,color:#0b2545
  classDef borde fill:#fdf0dd,stroke:#c87f1e,stroke-width:1.5px,color:#4a2a00
  classDef modulo fill:#ffffff,stroke:#aab2c0,stroke-width:1px,color:#1b2430
  classDef datos fill:#e6f4e8,stroke:#45863f,stroke-width:1.5px,color:#10300f
  classDef worker fill:#efe6f9,stroke:#6f4aa0,stroke-width:1.5px,color:#241036
  classDef obs fill:#fdeaef,stroke:#b84a63,stroke-width:1.5px,color:#3d0f1a
  classDef externo fill:#f0f0f0,stroke:#8c8c8c,stroke-width:1px,stroke-dasharray:5 3,color:#2b2b2b

  class MAUI,BO,POR,SEG cliente
  class TRF borde
  class M1,M2,M3,M4,M5,M6,M7,M8 modulo
  class PG,RD,MQ datos
  class W worker
  class OTEL obs
  class DEST,COM externo

  style API fill:#f6f7f9,stroke:#79839a,stroke-width:1.5px,color:#1b2430
```

## Leyenda

| Elemento | Significado |
| :---- | :---- |
| Rectángulo | Aplicación desplegable o módulo de negocio |
| Cilindro | Almacenamiento |
| Rectángulo doble | Broker de mensajes |
| Estadio punteado | Sistema externo a la plataforma |
| Flecha llena | Llamada directa |
| Flecha punteada | Salida asíncrona o telemetría |

## Regla de dependencias

`Domain ← Application ← Infrastructure`. Domain no referencia ningún proyecto; Application
depende solo de Domain y declara las interfaces que Infrastructure implementa. La API y el
Worker registran dependencias e invocan Handlers. Verificado con NetArchTest en el pipeline.

## Comunicación entre módulos

Dentro del proceso, síncrona por las interfaces públicas de cada módulo: hacia afuera un
módulo solo expone Commands, Queries y DTOs, nunca sus entidades ni sus repositorios.

Todo efecto hacia afuera se escribe como evento de integración en la tabla outbox, en la
misma transacción que el cambio, y se publica a RabbitMQ.
