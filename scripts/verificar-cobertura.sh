#!/usr/bin/env bash
# Opcional 7.2 (cobertura de pruebas): falla si Domain o Application tienen 70 % de líneas o menos.
#
#   scripts/verificar-cobertura.sh coverage/Cobertura.xml        # umbral 70
#   scripts/verificar-cobertura.sh coverage/Cobertura.xml 80     # otro umbral
set -euo pipefail

ARCHIVO=${1:?Falta el archivo Cobertura.xml}
UMBRAL=${2:-70}
FALLA=0

for capa in UltimaMilla.Domain UltimaMilla.Application; do
  tasa=$(xmllint --xpath "string(//package[@name='$capa']/@line-rate)" "$ARCHIVO")
  if [ -z "$tasa" ]; then
    echo "::error::No hay datos de cobertura para $capa"
    FALLA=1
    continue
  fi
  pct=$(awk -v t="$tasa" 'BEGIN { printf "%.1f", t * 100 }')
  if awk -v p="$pct" -v u="$UMBRAL" 'BEGIN { exit !(p > u) }'; then
    estado=OK
  else
    estado="POR DEBAJO"
    FALLA=1
  fi
  echo "$capa: $pct % de líneas (umbral > $UMBRAL %) -> $estado"
done

exit $FALLA
