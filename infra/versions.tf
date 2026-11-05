terraform {
  required_version = ">= 1.8"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.14"
    }
  }
}

# La suscripción sale de `az login` o de ARM_SUBSCRIPTION_ID; nunca va en el código.
provider "azurerm" {
  features {}
}
