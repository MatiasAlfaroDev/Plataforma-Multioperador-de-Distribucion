# Verificación de los monitoreos del 29/10 y del 5/11

Plan para comprobar, contra la letra (§10 y RNF 6.8 a 6.14), lo que se pide en los dos monitoreos:

| Monitoreo | Letra |
|---|---|
| 29/10 | Worker independiente; avisos a comercios con reintentos y cola de fallidos; tablero en tiempo real; **cambio de requerimientos** no adelantado |
| 5/11 | Solución en la nube con IaC, al menos dos instancias balanceadas y el tablero de observabilidad operativo |

Lo esencial es la parte de la nube (§3): se evalúa sobre el sistema desplegado, y los escenarios 1,
6 y 7 de la defensa (§9.3) se corren ahí.

Estado al 5/11/2026: **Hecho y verificado** = probado en local con evidencia. **Hecho, falta
verificar** = el código está, pero falta correrlo donde corresponde. **Falta** = no está implementado.

## 1. Worker, avisos y mensajería (RNF 6.8 y 6.9, RF 28)

| # | Objetivo | Estado | Cómo se verifica |
|---|---|---|---|
| 1.1 | Worker desplegado aparte de la API, consumiendo de la cola | Hecho y verificado | `docker compose ps worker`; el Worker no expone HTTP |
| 1.2 | Outbox: no se persiste un cambio sin su evento ni al revés | Hecho y verificado | Con RabbitMQ detenido (`docker compose stop rabbitmq`) se crea un envío y la API responde 201. Al levantarlo, el aviso llega igual. Las filas quedan en `OutboxMessage` mientras tanto |
| 1.3 | Idempotencia del consumidor | Hecho, falta verificar | Reintentar desde el panel de mensajes fallidos un mensaje ya entregado: el receptor no muestra un aviso nuevo y `entregas_aviso` no duplica la fila (índice único `SuscripcionId + ClaveEvento`) |
| 1.4 | Un aviso por cada cambio de estado | Hecho y verificado | Alta y recepción en depósito dejan `envio.creado` y `envio.en_deposito` en `receptor.<dominio>`. Falta recorrer despacho, entrega, extravío y devolución |
| 1.5 | Firma HMAC verificable por el receptor | Hecho y verificado | El receptor responde 401 si la firma no coincide. Probar con `curl` un POST con la firma alterada |
| 1.6 | Reintentos con espera creciente y cola de fallidos | Hecho y verificado | Con el receptor en `POST /modo/falla`, en ~2 min el aviso queda `Fallida/6` y el mensaje en `entregar-aviso_error` |
| 1.7 | Recuperación al volver el receptor (**escenario 6**) | Hecho y verificado en local | Receptor en `falla`, generar eventos, pasarlo a `ok`: en ≤3 min quedan `Entregada` sin intervención (verificado: 162 s). **Repetirlo en la nube** |
| 1.8 | Panel del comercio: historial y reenvío | Hecho, falta verificar | Portal → Avisos como `tienda.norte@demo.local`: listar, filtrar por Fallida y reenviar |
| 1.9 | El administrador consulta y reintenta los mensajes fallidos | Hecho y verificado | Backoffice como `admin.plataforma@demo.local` → Mensajes fallidos: lista `entregar-aviso_error` con el motivo y "Reintentar todos" la vacía |
| 1.10 | Receptor de prueba con escenarios de falla | Hecho y verificado | Modos `ok`, `falla`, `intermitente` y `lento`. `intermitente` entrega al tercer intento; `lento` cae por el timeout de 10 s |
| 1.11 | Tareas programadas sin duplicarse | Hecho, falta verificar | `docker compose up -d --scale worker=2`: en cada minuto aparece un solo "Reencolados…" en `docker compose logs worker` |

## 2. Tablero en tiempo real (RNF 6.10) y cambio de requerimientos — **falta**

| # | Objetivo | Estado | Qué hay que hacer |
|---|---|---|---|
| 2.1 | Tablero de operación actualizado en vivo con SignalR | **Falta** | No hay ningún hub. Según el informe: hub en la API, backplane en Redis (dos réplicas), grupos por operador con la misma autenticación por cookie, y el Backoffice suscripto. La fuente natural es `EstadoEnvioCambiado`: el Worker la consume y notifica por el backplane |
| 2.2 | Aislamiento por inquilino en el canal | **Falta** | Abrir el tablero como `admin.rapi` y como `admin.sur`: cada uno ve solo los cambios de su operador |
| 2.3 | Funciona con dos réplicas | **Falta** | Una conexión en cada réplica (cookie de afinidad o `--scale`): un cambio originado en una llega a los clientes de la otra |
| 2.4 | Cambio de requerimientos del 29/10 | **Falta definir** | Hace falta el texto del cambio que dio el docente para planificarlo e incorporarlo |

## 3. Nube con IaC (RNF 6.14) — lo esencial

El código está commiteado (`infra/`, `docker-compose.nube.yml`, job `desplegar`), pero **nunca se
aplicó contra Azure**. Validado: `tofu validate` en verde, y el perfil de nube corrido en local con
`DOMINIO=localhost` (HTTPS, redirección 80→443, entrypoint interno, Grafana con login, alta de envío
y aviso por HTTPS).

No hay dominio, así que se usa **sslip.io**: los hosts se derivan de la IP fija
(`portal.<ip-con-guiones>.sslip.io`) y no hay nada que registrar. Si aparece un dominio, alcanza con
la variable `dominio` del `.tfvars`.

Orden de ejecución (detalle en `infra/LEEME.md`):

| # | Paso | Criterio de aceptación |
|---|---|---|
| 3.1 | `az login`, `ARM_SUBSCRIPTION_ID`, clave SSH, secretos y `DESPLIEGUE_HABILITADO=true` en GitHub | `gh secret list` muestra los 8 secretos |
| 3.2 | `tofu plan -var-file=ambientes/prod.tfvars` | El plan crea 9 recursos sin errores de política (si falla la región, cambiar `region`) |
| 3.3 | `infra/reconstruir.sh` | El job `desplegar` queda en verde y `https://api.<dominio>/ready` responde 200 |
| 3.4 | HTTPS con certificado válido | `curl -v https://portal.<dominio>` sin `-k`: el emisor es Let's Encrypt |
| 3.5 | Las seis URLs de `tofu output urls` responden | Portal, Backoffice, API, Grafana (pide login), RabbitMQ y receptor |
| 3.6 | Deploy continuo | Un push a `main` con las pruebas en verde se despliega solo (ver el run en Actions) |
| 3.7 | Secretos fuera del código | `git grep -i password` no encuentra valores reales; el `.env` existe solo en la VM |
| 3.8 | Variables por ambiente | `tofu plan -var-file=ambientes/dev.tfvars` planifica `rg-ultimamilla-dev` sin tocar prod |
| 3.9 | **Escenario 7**: destruir y reconstruir | `tofu destroy` y después `infra/reconstruir.sh`, cronometrado; el sistema vuelve con los datos demo |
| 3.10 | Control y evidencia del costo | Captura de Cost analysis filtrado por `proyecto=ultimamilla`; destruir el ambiente al terminar cada sesión |

Riesgos que conviene probar temprano:
- **Política de regiones** de Azure for Students: puede no permitir `eastus2` o el tamaño `B2as_v2`.
- **Primer build en la VM** (2 vCPU): puede tardar bastante. Hay 4 GB de swap y `command_timeout: 30m`;
  si no alcanza, pasar a imágenes construidas en CI y publicadas en GHCR.
- **Límites de Let's Encrypt**: varias reconstrucciones el mismo día cambian la IP y piden
  certificados nuevos; no repetir el escenario 7 decenas de veces.
- La **app móvil** apunta a `10.0.2.2:8000`: para la demo en la nube hay que configurar
  `https://api.<dominio>` en `Mobile/Data/Constants.cs`.

## 4. Dos instancias balanceadas (RNF 6.13)

| # | Objetivo | Estado | Cómo se verifica |
|---|---|---|---|
| 4.1 | Dos réplicas de la API detrás de Traefik | Hecho y verificado | Panel "Réplicas reportando" = 2; "Solicitudes por réplica" reparte la carga |
| 4.2 | Continuidad ante la caída (**escenario 1**) | Hecho y verificado en local | `scripts/prueba-caida-instancia.sh`: 0 errores en 9306 peticiones con `docker stop` y 1 en 10389 con `--matar`. **Repetirlo en la VM** con `URL=https://api.<dominio>` |
| 4.3 | `/health`, `/ready` y `/live` | Hecho y verificado | Los tres responden 200; `/ready` pasa a 503 durante el apagado |
| 4.4 | Apagado controlado | Hecho y verificado | Mismo script con `docker stop` |
| 4.5 | API sin estado en memoria y Data Protection compartida | Hecho, falta verificar | Iniciar sesión, detener la réplica que atendió y seguir navegando sin volver a loguearse |
| 4.6 | Tareas programadas sin duplicar | Ver 1.11 | — |

## 5. Observabilidad operativa (RNF 6.12)

| # | Panel o capacidad exigida | Estado | Qué falta |
|---|---|---|---|
| 5.1 | Tiempo medio de respuesta, p95 y p99 | Hecho | Verificar en el Grafana de la nube |
| 5.2 | Tasa de errores | Hecho | — |
| 5.3 | **Profundidad de la cola** | **Falta** | El plugin `rabbitmq_prometheus` (puerto 15692) que el Collector raspe, y un panel con `rabbitmq_queue_messages` por cola, incluidas las `*_error` |
| 5.4 | **Sincronizaciones pendientes** | **Falta** | Métrica de los eventos móviles pendientes (lo que reporta la app al sincronizar, o lo pendiente del lado del servidor) y su panel |
| 5.5 | Avisos a comercios fallidos | Hecho | Verificar que muestra datos en la nube: la métrica la emite el Worker (`avisos_fallidos_total`) |
| 5.6 | Relación de aciertos del caché | Hecho | — |
| 5.7 | Correlación de punta a punta, de la app al Worker | Falta verificar | En Tempo, una traza de `POST /api/envios/{id}/eventos-movil` tiene que llegar hasta el consumidor del Worker y el POST al receptor. Confirmar que la app manda `traceparent` |
| 5.8 | Grafana accesible y protegido en la nube | Hecho, falta verificar | `https://grafana.<dominio>` pide usuario (`admin` / `GRAFANA_PASSWORD`) y el tablero técnico está provisionado |

## Orden sugerido

1. **Nube (§3)**: 3.1 a 3.5 primero, porque destraba todo lo demás que se verifica en la VM.
2. Paneles faltantes (5.3 y 5.4), así el tablero queda completo en la nube.
3. SignalR (§2.1 a 2.3) y el cambio de requerimientos (§2.4), en cuanto se tenga su texto.
4. Correr en la VM los escenarios 1, 6 y 7 y guardar la evidencia: salida de los scripts, capturas de
   Grafana y costo.
