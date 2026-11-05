# Infraestructura como código (RNF 6.14)

OpenTofu crea en Azure for Students una VM Linux (`Standard_B2as_v2`, 2 vCPU y 8 GB) con su red,
su grupo de seguridad (22, 80 y 443) y una IP pública fija. La VM levanta el mismo `docker compose`
que en local, con el override `docker-compose.nube.yml`: Traefik con Let's Encrypt, dos réplicas de
la API, el Worker y Grafana. Ver el informe, sección 2.

**Dominio.** No hace falta comprar uno. Sin `dominio` en el `.tfvars`, los nombres salen de la IP
con [sslip.io](https://sslip.io): con la IP `20.1.2.3` quedan `portal.20-1-2-3.sslip.io`,
`api.20-1-2-3.sslip.io`, etc., que resuelven a esa IP sin registrar nada, y Let's Encrypt emite
certificados válidos igual. Si más adelante hay un dominio propio, se pone `dominio = "midominio.uy"`:
OpenTofu crea la zona de Azure DNS con los registros `@` y `*`, y hay que delegarle en el
registrador los NS de la salida `name_servers`.

**Secretos.** Ninguno está en el código. La clave SSH entra por `TF_VAR_ssh_clave_publica` y las
claves de la aplicación están en GitHub (secretos del repositorio). El deploy las escribe en el
`.env` de la VM.

## Una sola vez

1. Instalar [OpenTofu](https://opentofu.org/docs/intro/install/), la CLI de Azure y `gh`.
2. Entrar a Azure y elegir la suscripción de estudiante:
   ```bash
   az login
   export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
   ```
   Azure for Students restringe las regiones por política. Si `apply` falla con
   `RequestDisallowedByPolicy`, cambiar `region` en el `.tfvars` por una de las permitidas:
   `az policy assignment list --query "[].parameters.listOfAllowedLocations.value" -o tsv`.
3. Clave SSH del deploy:
   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/ultimamilla -N "" -C deploy-ultimamilla
   export TF_VAR_ssh_clave_publica="$(cat ~/.ssh/ultimamilla.pub)"
   ```
4. Secretos y variables de GitHub Actions (el valor de cada clave lo elige el equipo):
   ```bash
   gh secret set VM_USUARIO --body ultimamilla
   gh secret set VM_CLAVE_SSH < ~/.ssh/ultimamilla
   gh secret set DEMO_PASSWORD        # Identity: 8+ caracteres, mayúscula, minúscula, dígito y símbolo
   gh secret set POSTGRES_PASSWORD
   gh secret set RABBITMQ_PASSWORD
   gh secret set GRAFANA_PASSWORD
   gh secret set FIREBASE_JSON < docker/secretos/firebase-cuenta-servicio.json   # opcional (push)
   gh variable set DESPLIEGUE_HABILITADO --body true
   ```
   Evitar `$` en las claves: el `.env` de compose lo interpreta.

## Crear, reconstruir y destruir

| Operación | Comando |
|---|---|
| Planificar | `cd infra && tofu init && tofu plan -var-file=ambientes/prod.tfvars` |
| Crear o reconstruir **y desplegar** | `infra/reconstruir.sh` (o `infra/reconstruir.sh dev`) |
| Solo aplicar | `tofu apply -var-file=ambientes/prod.tfvars` |
| Destruir todo el ambiente | `tofu destroy -var-file=ambientes/prod.tfvars` |
| Ver URLs e IP | `tofu output` |
| Entrar a la VM | `ssh -i ~/.ssh/ultimamilla ultimamilla@$(tofu output -raw ip_publica)` |

`reconstruir.sh` hace el `apply`, actualiza el secreto `VM_HOST` con la IP nueva y dispara el
workflow `CI`. Su job `desplegar` espera a que cloud-init termine de instalar Docker, actualiza el
repo, escribe el `.env`, levanta el compose y verifica `https://api.<dominio>/ready`. Desde ahí,
cada push a `main` con las pruebas en verde se despliega solo.

El escenario 7 de la defensa es `tofu destroy` seguido de `infra/reconstruir.sh`. Hay que tener en
cuenta que la base se reconstruye vacía y el inicializador vuelve a sembrar los datos de
demostración.

## Ambientes

`ambientes/prod.tfvars` y `ambientes/dev.tfvars` se diferencian en el nombre de cada recurso
(`rg-ultimamilla-<ambiente>`), así que pueden convivir. El estado de OpenTofu es local, en
`infra/terraform.tfstate`, y no se versiona: lo tiene quien hizo el `apply`.

## Costo (control y evidencia)

La VM es lo único que cobra en serio: del orden de USD 2 por día encendida. Hay que confirmarlo en
la [calculadora](https://azure.microsoft.com/pricing/calculator/) para la región elegida. A eso se
suman el disco de 64 GB y la IP fija, unos centavos por día. Para gastar el crédito de estudiante
solo en la defensa:

- **Destruir al terminar de usarlo** (`tofu destroy`): reconstruir es un comando.
- La evidencia está en Cost Management del portal (*Cost analysis* filtrado por el grupo
  `rg-ultimamilla-prod`) o en `az consumption usage list --query "[?contains(instanceName,'ultimamilla')]"`.
- Todos los recursos llevan las etiquetas `proyecto=ultimamilla` y `ambiente=<ambiente>` para filtrar el costo.
