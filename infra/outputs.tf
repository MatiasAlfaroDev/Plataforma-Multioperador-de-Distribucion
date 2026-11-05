output "ip_publica" {
  description = "IP de la VM: va en el secreto VM_HOST de GitHub Actions."
  value       = azurerm_public_ip.vm.ip_address
}

output "dominio" {
  description = "Va en DOMINIO del .env de la VM (lo escribe el deploy)."
  value       = local.dominio
}

output "name_servers" {
  description = "Solo con dominio propio: servidores de nombres a configurar en el registrador."
  value       = one(azurerm_dns_zone.dominio[*].name_servers)
}

output "ssh" {
  value = "ssh ${var.usuario_admin}@${azurerm_public_ip.vm.ip_address}"
}

output "urls" {
  value = [for h in ["portal", "backoffice", "api", "grafana", "rabbit", "receptor"] : "https://${h}.${local.dominio}"]
}
