# Secretos locales

Esta carpeta se monta en la API como `/run/secretos` (solo lectura). Todo lo que pongas acá,
salvo este archivo, está en `.gitignore` y **nunca se sube al repo**.

## Notificaciones push (Firebase)

1. En la consola de Firebase: **Configuración del proyecto → Cuentas de servicio → Generar nueva clave privada**.
2. Guardá el archivo descargado acá con este nombre exacto: `firebase-cuenta-servicio.json`.
3. `docker compose up --build`.

Sin el archivo, la API arranca igual y el push queda desactivado: `POST /api/notificaciones/prueba`
responde 400 con "El servidor no tiene configurado Firebase".
