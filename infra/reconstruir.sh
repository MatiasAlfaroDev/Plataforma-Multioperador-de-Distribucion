#!/usr/bin/env bash
# Reconstruye el ambiente con un comando (RNF 6.14, escenario 7): crea o actualiza la infraestructura,
# le pasa la IP nueva a GitHub Actions y dispara el deploy.
#
#   infra/reconstruir.sh            # prod
#   infra/reconstruir.sh dev
set -euo pipefail
cd "$(dirname "$0")"
ambiente=${1:-prod}

: "${TF_VAR_ssh_clave_publica:?Exportá TF_VAR_ssh_clave_publica con la clave pública SSH (ver LEEME.md).}"

tofu init -input=false
tofu apply -auto-approve -input=false -var-file="ambientes/$ambiente.tfvars"

# La IP cambia en cada reconstrucción: el deploy y el dominio sslip.io salen de ella.
gh secret set VM_HOST --body "$(tofu output -raw ip_publica)"
gh workflow run CI --ref main

echo "Deploy disparado; seguilo con: gh run watch"
tofu output urls
