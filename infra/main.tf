locals {
  # Sin dominio propio, los nombres salen de la IP fija con sslip.io (portal.20-1-2-3.sslip.io resuelve
  # a 20.1.2.3): no hay que registrar nada y Let's Encrypt emite certificados válidos igual.
  dominio = var.dominio != "" ? var.dominio : "${replace(azurerm_public_ip.vm.ip_address, ".", "-")}.sslip.io"

  etiquetas = {
    proyecto = "ultimamilla"
    ambiente = var.ambiente
  }
}

# Todo el ambiente cuelga de este grupo: `tofu destroy` lo borra entero (escenario 7).
resource "azurerm_resource_group" "rg" {
  name     = "rg-ultimamilla-${var.ambiente}"
  location = var.region
  tags     = local.etiquetas
}

resource "azurerm_virtual_network" "vnet" {
  name                = "vnet-ultimamilla-${var.ambiente}"
  address_space       = ["10.20.0.0/16"]
  location            = var.region
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.etiquetas
}

resource "azurerm_subnet" "subnet" {
  name                 = "subnet-ultimamilla-${var.ambiente}"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.20.1.0/24"]
}

# Entrada: SSH para el deploy, 80 para el desafío de Let's Encrypt (Traefik redirige a 443) y 443.
resource "azurerm_network_security_group" "nsg" {
  name                = "nsg-ultimamilla-${var.ambiente}"
  location            = var.region
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.etiquetas

  security_rule {
    name                       = "ssh"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "22"
    source_address_prefix      = var.ip_admin
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "http"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "80"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "https"
    priority                   = 120
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }
}

# Fija: los nombres DNS apuntan a ella.
resource "azurerm_public_ip" "vm" {
  name                = "pip-ultimamilla-${var.ambiente}"
  location            = var.region
  resource_group_name = azurerm_resource_group.rg.name
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = local.etiquetas
}

resource "azurerm_network_interface" "nic" {
  name                = "nic-ultimamilla-${var.ambiente}"
  location            = var.region
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.etiquetas

  ip_configuration {
    name                          = "principal"
    subnet_id                     = azurerm_subnet.subnet.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.vm.id
  }
}

resource "azurerm_network_interface_security_group_association" "nic" {
  network_interface_id      = azurerm_network_interface.nic.id
  network_security_group_id = azurerm_network_security_group.nsg.id
}

resource "azurerm_linux_virtual_machine" "vm" {
  name                = "vm-ultimamilla-${var.ambiente}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.region
  size                = var.tamano_vm
  admin_username      = var.usuario_admin

  admin_ssh_key {
    username   = var.usuario_admin
    public_key = var.ssh_clave_publica
  }

  disable_password_authentication = true

  network_interface_ids = [
    azurerm_network_interface.nic.id,
  ]

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = "StandardSSD_LRS"
    disk_size_gb         = 64
  }

  source_image_reference {
    publisher = "Canonical"
    offer     = "ubuntu-24_04-lts"
    sku       = "server"
    version   = "latest"
  }

  custom_data = base64encode(templatefile("${path.module}/cloud-init.yaml", {
    usuario     = var.usuario_admin
    repositorio = var.repositorio
    rama        = var.rama
  }))

  tags = local.etiquetas
}
