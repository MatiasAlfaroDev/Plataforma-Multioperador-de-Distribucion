# Pruebas de carga (opcional 7.2)

Tres escenarios con [k6](https://k6.io), con los objetivos de nivel de servicio escritos **antes** de correr
(son los `thresholds` de cada script: si no se cumplen, k6 termina con error). Se corren con la imagen oficial,
sin instalar nada.

| Escenario | Script | Carga | Duración | Objetivos (SLO) |
| :-- | :-- | :-- | :-- | :-- |
| Seguimiento público | `seguimiento.js` | Rampa por tasa de llegada hasta `TASA` pedidos/s (50) | 1 min de rampa, 3 sostenido, 30 s de bajada | p95 < 300 ms, p99 < 800 ms, errores < 1 % |
| Despachadores en el Backoffice | `despacho.js` | Rampa hasta `USUARIOS` concurrentes (30), cada uno lista envíos y el tablero cada 1–3 s | 1 + 3 + 0,5 min | p95 < 500 ms y p99 < 1 s en `/api/envios` y `/api/tablero`, errores < 1 % |
| **Regreso al depósito** (exigido por la letra) | `sincronizacion-deposito.js` | `REPARTIDORES` teléfonos (40) que sincronizan **a la vez**: `LOTES` lotes de 200 posiciones (5), informe de pendientes y descarga de la hoja | Una ráfaga, máximo 2 min | p95 < 1 s y p99 < 2 s por lote, errores < 1 %, los 40 terminan |

Por qué esos números: el seguimiento es la consulta más frecuente y la más expuesta (la abre el destinatario
desde el celular), y está en caché; el Backoffice es interactivo, medio segundo es el límite de "responde al
toque"; un lote de 200 posiciones es una escritura grande y el repartidor espera en el depósito, un segundo es
aceptable.

## Antes de correr

La limitación de tasa (RNF 6.3) cuenta por IP y por usuario, y k6 sale de una sola IP con una sola sesión:
sin apagarla, la prueba mide los 429. Se apaga solo en la API y solo durante la prueba:

```bash
LIMITAR_TASA=false docker compose up -d api          # en la VM, con los -f de docker-compose.nube.yml
```

Al terminar, `docker compose up -d api` sin la variable la deja como estaba.

## Cómo correr

Desde la raíz del repo, con el entorno levantado (`docker compose up`). `DEMO_PASSWORD` es la del `.env`.

```bash
docker run --rm -v "$PWD/pruebas/carga:/scripts" --add-host api.localhost:host-gateway \
  -e DEMO_PASSWORD -e BASE_URL=http://api.localhost:8000 \
  grafana/k6:0.57.0 run /scripts/sincronizacion-deposito.js
```

Contra el ambiente remoto, `BASE_URL=https://api.<ip-con-guiones>.sslip.io` (sin `--add-host`).

Variables: `TASA`, `USUARIOS`, `REPARTIDORES`, `LOTES`, `USUARIO` (otro usuario de demo) y, para el
seguimiento, `TOKENS` con tokens reales separados por comas (los enlaces de los correos en Mailpit); sin
ellos se consultan tokens inexistentes y se mide el camino del 404.

Mientras corre, el tablero técnico de Grafana muestra latencia, errores, réplicas y la profundidad de las
colas; cada teléfono del escenario de sincronización manda su `X-Correlation-Id` (`k6-<vu>-<hora>`), así se
puede buscar una sincronización puntual en los logs.

## Resultados

Completar después de cada corrida (pegando el resumen de k6 y una captura del tablero técnico).

| Fecha | Ambiente | Escenario | p95 | p99 | Errores | ¿Cumple? | Observaciones |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| | | | | | | | |
