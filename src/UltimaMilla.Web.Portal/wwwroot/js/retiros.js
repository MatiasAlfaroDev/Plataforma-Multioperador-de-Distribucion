// Retiros a domicilio (RF 37 y 41): listado, solicitud y enlace público. Mismo esquema que avisos.js.

window.retirosPortal = {
    // Aclaración 3.2: { operadorId: { "retiro-domicilio": true, ... } } para cada operador del comercio.
    async banderas() {
        const respuesta = await fetch("/api/banderas", { method: "GET", credentials: "same-origin" });
        return respuesta.ok ? await respuesta.json() : {};
    },

    async listar() {
        const respuesta = await fetch("/api/retiros", {
            method: "GET",
            credentials: "same-origin"
        });

        if (!respuesta.ok) {
            throw new Error(`No se pudieron cargar los retiros. HTTP ${respuesta.status}`);
        }

        return await respuesta.json();
    },

    async solicitar(datos) {
        const respuestaToken = await fetch("/api/antiforgery/token", {
            method: "GET",
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!respuestaToken.ok) {
            return { exito: false, mensaje: "No se pudo obtener el token de seguridad." };
        }

        const { token } = await respuestaToken.json();

        const respuesta = await fetch("/api/retiros", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": token
            },
            credentials: "same-origin",
            body: JSON.stringify(datos)
        });

        if (respuesta.status === 201) {
            return { exito: true, mensaje: "Retiro solicitado." };
        }

        return { exito: false, mensaje: await mensajeDeProblema(respuesta) };
    },

    async enlacePublico(id) {
        const respuesta = await fetch(`/api/retiros/${encodeURIComponent(id)}/enlace-publico`, {
            method: "GET",
            credentials: "same-origin"
        });

        if (!respuesta.ok) {
            throw new Error(`No se pudo generar el enlace. HTTP ${respuesta.status}`);
        }

        const { url } = await respuesta.json();
        return new URL(url, window.location.origin).href;
    },

    async generarClaveApi() {
        const respuestaToken = await fetch("/api/antiforgery/token", {
            method: "GET",
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!respuestaToken.ok) {
            throw new Error("No se pudo obtener el token de seguridad.");
        }

        const { token } = await respuestaToken.json();

        const respuesta = await fetch("/api/comercio/usuarios/clave-api", {
            method: "POST",
            headers: { "X-CSRF-TOKEN": token },
            credentials: "same-origin"
        });

        if (!respuesta.ok) {
            throw new Error(await mensajeDeProblema(respuesta));
        }

        return (await respuesta.json()).clave;
    }
};
