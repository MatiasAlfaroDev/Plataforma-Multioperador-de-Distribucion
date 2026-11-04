window.autenticacionPortal = {
    async login(email, password) {
        const respuesta = await fetch("/api/auth/login", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            credentials: "same-origin",
            body: JSON.stringify({ email, password })
        });

        if (!respuesta.ok) {
            if (respuesta.status === 401) {
                throw new Error("Correo o contraseña incorrectos.");
            }

            throw new Error("No se pudo iniciar sesión.");
        }

        return await respuesta.json();
    },

    async obtenerMembresias() {
        const respuesta = await fetch("/api/auth/membresias", {
            method: "GET",
            credentials: "same-origin"
        });

        if (!respuesta.ok) {
            throw new Error("No se pudieron obtener las membresías.");
        }

        return await respuesta.json();
    },

    async seleccionarOperador(membresiaId) {
        const respuesta = await fetch("/api/auth/seleccionar-operador", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            credentials: "same-origin",
            body: JSON.stringify({ membresiaId })
        });

        if (!respuesta.ok) {
            throw new Error("No se pudo seleccionar el operador.");
        }

        return await respuesta.json();
    },
    async obtenerCuentasComerciales() {
    const respuesta = await fetch("/api/cuentas-comerciales", {
        method: "GET",
        credentials: "same-origin"
    });

    if (!respuesta.ok) {
        throw new Error(
            `No se pudieron cargar las cuentas comerciales. HTTP ${respuesta.status}`
        );
    }

    return await respuesta.json();
},
async obtenerZonas(operadorId) {
    const respuesta = await fetch(
        `/api/zonas?operadorId=${encodeURIComponent(operadorId)}`,
        {
            method: "GET",
            credentials: "same-origin"
        }
    );

    if (!respuesta.ok) {
        throw new Error(
            `No se pudieron cargar las zonas. HTTP ${respuesta.status}`
        );
    }

    return await respuesta.json();
},
async crearEnvio(datos) {
    // 1. Obtener el token de seguridad
    const respuestaToken = await fetch("/api/antiforgery/token", {
        method: "GET",
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!respuestaToken.ok) {
        return {
            exito: false,
            mensaje: "No se pudo obtener el token de seguridad. Iniciá sesión nuevamente."
        };
    }

    const { token } = await respuestaToken.json();

    // 2. Crear el envío con el token
    const respuesta = await fetch("/api/envios", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token
        },
        credentials: "same-origin",
        body: JSON.stringify(datos)
    });

    // 3. Procesar la respuesta
    if (respuesta.status === 201) {
        return {
            exito: true,
            mensaje: "Envío dado de alta correctamente."
        };
    }

    let mensaje = `La API respondió HTTP ${respuesta.status}.`;

    try {
        const problema = await respuesta.json();

        if (problema.errors) {
            mensaje = Object.values(problema.errors).flat().join(" ");
        } else if (problema.detail) {
            mensaje = problema.detail;
        } else if (problema.title) {
            mensaje = problema.title;
        }
    } catch {
        // Conservamos el mensaje HTTP original.
    }

    
    return {
        exito: false,
        mensaje
    };
},

async importarEnviosCsv(cuentaComercialId) {
    const archivo = document.getElementById("archivoCsv")?.files?.[0];

    if (!cuentaComercialId) {
        return {
            exito: false,
            mensaje: "Seleccioná una cuenta comercial."
        };
    }

    if (!archivo) {
        return {
            exito: false,
            mensaje: "Seleccioná un archivo CSV."
        };
    }

    const maxBytes = 5 * 1024 * 1024;

    if (archivo.size === 0 || archivo.size > maxBytes) {
        return {
            exito: false,
            mensaje: "El archivo debe tener contenido y no superar los 5 MB."
        };
    }

    if (!archivo.name.toLowerCase().endsWith(".csv")) {
        return {
            exito: false,
            mensaje: "El archivo debe tener extensión .csv."
        };
    }

    // Obtener el token CSRF, igual que en el alta individual.
    const respuestaToken = await fetch("/api/antiforgery/token", {
        method: "GET",
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!respuestaToken.ok) {
        return {
            exito: false,
            mensaje: "No se pudo obtener el token de seguridad."
        };
    }

    const { token } = await respuestaToken.json();

    // Los nombres coinciden con los parámetros del endpoint.
    const datos = new FormData();
    datos.append("archivo", archivo);
    datos.append("cuentaComercialId", cuentaComercialId);

    const respuesta = await fetch("/api/envios/importar-csv", {
        method: "POST",
        headers: {
            "X-CSRF-TOKEN": token
        },
        credentials: "same-origin",
        body: datos
    });

    // No establecer Content-Type manualmente:
    // el navegador agrega el boundary de multipart/form-data.
    if (respuesta.ok) {
        return {
            exito: true,
            mensaje: "Importación procesada.",
            resultado: await respuesta.json()
        };
    }

    let mensaje = `La API respondió HTTP ${respuesta.status}.`;

    try {
        const problema = await respuesta.json();

        if (problema.errors) {
            mensaje = Object.values(problema.errors).flat().join(" ");
        } else if (problema.detail) {
            mensaje = problema.detail;
        } else if (problema.title) {
            mensaje = problema.title;
        }
    } catch {
        // Mantener el mensaje HTTP.
    }

    return {
        exito: false,
        mensaje
    };
}
};
