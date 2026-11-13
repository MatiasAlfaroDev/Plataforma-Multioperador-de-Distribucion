#!/usr/bin/env bash
# Falla si la cobertura de líneas de Domain o Application no supera el umbral.
# Uso: scripts/verificar-cobertura.sh [Cobertura.xml] [umbral en %]
# Lee el XML con awk, así no hace falta instalar nada en el runner.
set -euo pipefail
 
ARCHIVO="${1:-coverage/Cobertura.xml}"
UMBRAL="${2:-70}"
CAPAS=("UltimaMilla.Domain" "UltimaMilla.Application")
 
if [ ! -f "$ARCHIVO" ]; then
  echo "No existe $ARCHIVO" >&2
  exit 1
fi
 
fallo=0
for capa in "${CAPAS[@]}"; do
  # Busca <package name="CAPA" line-rate="0.7089" ...> y devuelve el porcentaje.
  porcentaje=$(awk -v capa="$capa" '
    /<package / {
      if (match($0, /name="[^"]*"/)) {
        nombre = substr($0, RSTART + 6, RLENGTH - 7)
        if (nombre == capa && match($0, /line-rate="[^"]*"/)) {
          print substr($0, RSTART + 11, RLENGTH - 12) * 100
          exit
        }
      }
    }' "$ARCHIVO")
 
  if [ -z "$porcentaje" ]; then
    echo "::error::No se encontró la capa $capa en $ARCHIVO"
    fallo=1
    continue
  fi
 
  if awk -v p="$porcentaje" -v u="$UMBRAL" 'BEGIN { exit !(p > u) }'; then
    printf '%s: %.1f %% (umbral %s %%) OK\n' "$capa" "$porcentaje" "$UMBRAL"
  else
    printf '::error::%s: %.1f %% no supera el umbral de %s %%\n' "$capa" "$porcentaje" "$UMBRAL"
    fallo=1
  fi
done
 
exit "$fallo"