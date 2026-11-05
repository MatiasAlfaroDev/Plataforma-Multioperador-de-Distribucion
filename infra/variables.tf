variable "ambiente" {
  description = "Nombre del ambiente (dev, prod): va en el nombre de cada recurso."
  type        = string
}

variable "region" {
  description = "Región de Azure. Azure for Students limita las regiones permitidas por política."
  type        = string
  default     = "eastus2"
}

variable "tamano_vm" {
  description = "Tamaño de la VM: el compose completo entra en 2 vCPU y 8 GB (informe 2.3)."
  type        = string
  default     = "Standard_B2as_v2"
}

variable "dominio" {
  description = "Dominio propio (opcional). Vacío: se usa <ip>.sslip.io. Con valor, se crea su zona de Azure DNS y hay que delegarle los NS en el registrador."
  type        = string
  default     = ""
}

variable "usuario_admin" {
  description = "Usuario Linux de la VM, el mismo con el que entra el deploy de GitHub Actions."
  type        = string
  default     = "ultimamilla"
}

variable "ssh_clave_publica" {
  description = "Clave pública SSH del usuario administrador (contenido del .pub)."
  type        = string
}

variable "ip_admin" {
  description = "Origen permitido para SSH (CIDR). '*' lo abre a todos: hace falta para el deploy desde GitHub Actions."
  type        = string
  default     = "*"
}

variable "repositorio" {
  description = "Repositorio público que clona la VM al crearse."
  type        = string
  default     = "https://github.com/MatiasAlfaroDev/Plataforma-Multioperador-de-Distribucion.git"
}

variable "rama" {
  description = "Rama que despliega la VM."
  type        = string
  default     = "main"
}
