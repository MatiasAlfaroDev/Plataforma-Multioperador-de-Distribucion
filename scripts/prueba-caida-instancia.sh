#!/usr/bin/env bash
# Escenario 1 de la defensa (RNF 6.13): genera tráfico contra la API, detiene una réplica a mitad
# de camino y cuenta las respuestas que no fueron 200. Pega a /live y no a /ready, porque /ready
# falla a propósito en la réplica que se apaga (DrenajeAlApagar).
#
#   scripts/prueba-caida-instancia.sh                      # local, apagado controlado (docker stop)
#   scripts/prueba-caida-instancia.sh --matar              # caída abrupta (docker kill)
#   URL=https://api.midominio.uy scripts/prueba-caida-instancia.sh   # en la VM
set -euo pipefail

URL=${URL:-http://api.localhost:8000}
SEGUNDOS=${SEGUNDOS:-30}
accion=stop
[[ "${1:-}" == "--matar" ]] && accion=kill

cd "$(dirname "$0")/.."
replica=$(docker compose ps -q api | head -1)
[[ -n "$replica" ]] || { echo "No hay réplicas de api corriendo."; exit 1; }

resultados=$(mktemp)
fin=$(( $(date +%s) + SEGUNDOS ))
( while (( $(date +%s) < fin )); do
    curl -s -o /dev/null -w '%{http_code}\n' --max-time 5 "$URL/live" >> "$resultados" || echo 000 >> "$resultados"
  done ) &
trafico=$!

sleep 5
echo "docker $accion de la réplica ${replica:0:12}…"
docker "$accion" "$replica" > /dev/null
wait "$trafico"

total=$(wc -l < "$resultados")
fallidas=$(grep -vc '^200$' "$resultados" || true)
echo "Peticiones: $total; no 200: $fallidas"
sort "$resultados" | uniq -c

echo "Levantando la réplica otra vez…"
docker start "$replica" > /dev/null
(( fallidas == 0 ))
