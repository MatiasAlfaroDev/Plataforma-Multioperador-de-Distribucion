#!/usr/bin/env bash
# Pruebas de resiliencia (opcional 7.4): interrumpe a propósito una dependencia mientras hay tráfico y cuenta
# cómo respondió la API antes, durante y después. Lo esperado y lo observado se documentan en
# Docs/Pruebas-Resiliencia.md.
#
#   scripts/prueba-resiliencia.sh redis        # caché caída
#   scripts/prueba-resiliencia.sh rabbitmq     # cola caída (mirar también la tabla del outbox)
#   scripts/prueba-resiliencia.sh postgres     # base caída
#   scripts/prueba-resiliencia.sh receptor     # el sistema del comercio falla (servicio externo)
#   SEGUNDOS=60 URL=https://api.<ip>.sslip.io scripts/prueba-resiliencia.sh redis   # en la VM
set -euo pipefail

dependencia=${1:-}
URL=${URL:-http://api.localhost:8000}
RECEPTOR=${RECEPTOR:-http://receptor.localhost:8000}
SEGUNDOS=${SEGUNDOS:-30}

cd "$(dirname "$0")/.."

case "$dependencia" in
  redis|rabbitmq|postgres)
    cortar()   { docker compose stop "$dependencia" > /dev/null; }
    restaurar() { docker compose start "$dependencia" > /dev/null; } ;;
  receptor)
    # Un servicio externo no se apaga desde acá: se lo pone a responder 500 (modo del receptor de prueba).
    cortar()   { curl -s -X POST "$RECEPTOR/modo/falla" > /dev/null; }
    restaurar() { curl -s -X POST "$RECEPTOR/modo/ok" > /dev/null; } ;;
  *) echo "Uso: $0 redis|rabbitmq|postgres|receptor"; exit 1 ;;
esac

# Mensajes esperando en el outbox (RNF 6.8): con RabbitMQ caído tienen que acumularse y vaciarse al volver.
outbox() {
  docker compose exec -T postgres psql -U ultimamilla -d ultimamilla -tAc 'select count(*) from "OutboxMessage"' 2>/dev/null \
    | tr -d '[:space:]' || echo "?"
}

resultados=$(mktemp)
trafico() {
  # /ready: si la réplica se considera sana. Seguimiento con un token inventado: pasa por la caché y la base
  # y responde 404, así no hace falta sesión ni datos.
  while [[ -f "$resultados.activo" ]]; do
    local listo seg
    listo=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$URL/ready" || echo 000)
    seg=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$URL/api/seguimiento/resiliencia-$RANDOM" || echo 000)
    echo "$(cat "$resultados.fase") ready=$listo seguimiento=$seg" >> "$resultados"
    sleep 0.5
  done
}

touch "$resultados.activo"
echo antes > "$resultados.fase"
trafico &

echo "Outbox antes: $(outbox)"
sleep 5

echo "Cortando $dependencia por ${SEGUNDOS}s…"
echo durante > "$resultados.fase"
cortar
[[ "$dependencia" == rabbitmq ]] && echo "Con RabbitMQ caído, cambiá el estado de un envío desde el Backoffice: tiene que funcionar."
sleep "$SEGUNDOS"
[[ "$dependencia" != postgres ]] && echo "Outbox durante: $(outbox)"

echo "Restaurando $dependencia…"
restaurar
echo despues > "$resultados.fase"
sleep 15
echo "Outbox después: $(outbox)"

rm -f "$resultados.activo"
wait

for f in antes durante despues; do
  echo "--- $f"
  grep "^$f " "$resultados" | cut -d' ' -f2- | sort | uniq -c
done
rm -f "$resultados" "$resultados.fase"
