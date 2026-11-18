// Cuenta corriente y liquidaciones del comercio (letra 4, Portal). Mismo esquema que retiros.js:
// la llamada sale del navegador con la cookie de sesión.

window.cuentaCorrientePortal = {
    async obtener(desde, hasta) {
        const parametros = new URLSearchParams();
        if (desde) parametros.set("desde", desde);
        if (hasta) parametros.set("hasta", hasta);

        const respuesta = await fetch(`/api/cuenta-corriente?${parametros}`, {
            method: "GET",
            credentials: "same-origin"
        });

        if (!respuesta.ok) {
            throw new Error(`No se pudo cargar la cuenta corriente. HTTP ${respuesta.status}`);
        }

        return await respuesta.json();
    }
};
