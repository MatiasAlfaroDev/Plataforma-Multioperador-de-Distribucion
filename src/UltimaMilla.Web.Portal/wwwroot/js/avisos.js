// Avisos a comercios (RNF 6.9): historial, reenvío y suscripciones. Mismo esquema que autenticacion.js.

// Arma un mensaje legible a partir del ProblemDetails que devuelve la API.
async function mensajeDeProblema(respuesta) {
    try {
        const problema = await respuesta.json();

        if (problema.errors) {
            return Object.values(problema.errors).flat().join(" ");
        }

        return problema.detail ?? problema.title ?? `La API respondió HTTP ${respuesta.status}.`;
    } catch {
        return `La API respondió HTTP ${respuesta.status}.`;
    }
}

window.avisosPortal = {
    async listarAvisos() {
        const respuesta = await fetch("/api/avisos", {
            method: "GET",
            credentials: "same-origin"
        });

        if (!respuesta.ok) {
            throw new Error(`No se pudieron cargar los avisos. HTTP ${respuesta.status}`);
        }

        return await respuesta.json();
    },

    async reenviar(id) {
        const respuesta = await fetch(`/api/avisos/${encodeURIComponent(id)}/reenviar`, {
            method: "POST",
            credentials: "same-origin"
        });

        if (respuesta.status === 204) {
            return {
                exito: true,
                mensaje: "Aviso reenviado: el Worker lo entrega en unos segundos."
            };
        }

        return {
            exito: false,
            mensaje: await mensajeDeProblema(respuesta)
        };
    },

    async listarSuscripciones() {
        const respuesta = await fetch("/api/suscripciones-aviso", {
            method: "GET",
            credentials: "same-origin"
        });

        if (!respuesta.ok) {
            throw new Error(`No se pudieron cargar las suscripciones. HTTP ${respuesta.status}`);
        }

        return await respuesta.json();
    },

    async crearSuscripcion(datos) {
        const respuesta = await fetch("/api/suscripciones-aviso", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            credentials: "same-origin",
            body: JSON.stringify(datos)
        });

        if (respuesta.status === 201) {
            return {
                exito: true,
                mensaje: "Suscripción creada."
            };
        }

        return {
            exito: false,
            mensaje: await mensajeDeProblema(respuesta)
        };
    }
};
