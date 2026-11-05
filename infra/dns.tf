# Solo con dominio propio (var.dominio): el registrador tiene que delegarle los NS de la salida `name_servers`.
# Sin él, sslip.io resuelve los nombres a partir de la IP y no hace falta ninguna zona.
resource "azurerm_dns_zone" "dominio" {
  count               = var.dominio == "" ? 0 : 1
  name                = var.dominio
  resource_group_name = azurerm_resource_group.rg.name
}

# El comodín cubre portal, backoffice, api, grafana, rabbit y receptor; Traefik enruta por Host.
resource "azurerm_dns_a_record" "raiz" {
  count               = var.dominio == "" ? 0 : 1
  name                = "@"
  zone_name           = azurerm_dns_zone.dominio[0].name
  resource_group_name = azurerm_resource_group.rg.name
  ttl                 = 300
  records             = [azurerm_public_ip.vm.ip_address]
}

resource "azurerm_dns_a_record" "comodin" {
  count               = var.dominio == "" ? 0 : 1
  name                = "*"
  zone_name           = azurerm_dns_zone.dominio[0].name
  resource_group_name = azurerm_resource_group.rg.name
  ttl                 = 300
  records             = [azurerm_public_ip.vm.ip_address]
}
