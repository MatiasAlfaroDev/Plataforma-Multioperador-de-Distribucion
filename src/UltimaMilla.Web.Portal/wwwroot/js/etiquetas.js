
window.etiquetasPortal = {
    imprimir(datos) {
        if (!datos || !Array.isArray(datos.bultos) ||
            datos.bultos.length === 0) {
            throw new Error("El envío no tiene bultos para imprimir.");
        }

        const escapar = valor => String(valor ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");

        if (!window.qrPortal) {
            throw new Error("No se cargó el generador de códigos QR.");
        }

        const etiquetas = datos.bultos.map((bulto, indice) => `
            <section class="etiqueta">
                <header>
                    <h1>ÚLTIMA MILLA</h1>
                    <strong>${escapar(datos.operador)}</strong>
                </header>

                <div class="referencia">
                    <small>REFERENCIA DEL ENVÍO</small>
                    <h2>${escapar(datos.referencia)}</h2>
                </div>

                <div class="campo">
                    <small>DESTINATARIO</small>
                    <strong>${escapar(datos.destinatario)}</strong>
                </div>

                <div class="campo">
                    <small>DIRECCIÓN DE ENTREGA</small>
                    <strong>${escapar(datos.direccion)}</strong>
                </div>

                <div class="identificacion">
                    ${window.qrPortal.svg(bulto.identificacion, 30)}
                    <div>
                        <small>IDENTIFICACIÓN DEL BULTO</small>
                        <h2>${escapar(bulto.identificacion)}</h2>
                        <small>Escaneá el QR con la app del repartidor</small>
                    </div>
                </div>

                <footer>
                    <span>Bulto ${indice + 1} de ${datos.bultos.length}</span>
                    <span>${escapar(datos.modalidad)}</span>
                </footer>
            </section>
        `).join("");

        const ventana = window.open("", "_blank");

        if (!ventana) {
            throw new Error(
                "El navegador bloqueó la ventana de impresión."
            );
        }

        ventana.document.open();
        ventana.document.write(`
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <meta charset="UTF-8">
                <title>Etiquetas - ${escapar(datos.referencia)}</title>
                <style>
                    @page {
                        size: 100mm 150mm;
                        margin: 0;
                    }

                    * {
                        box-sizing: border-box;
                    }

                    body {
                        margin: 0;
                        font-family: Arial, sans-serif;
                        color: #111;
                        background: white;
                    }

                    .etiqueta {
                        width: 100mm;
                        height: 150mm;
                        padding: 9mm;
                        display: flex;
                        flex-direction: column;
                        gap: 5mm;
                        break-after: page;
                        page-break-after: always;
                    }

                    .etiqueta:last-child {
                        break-after: auto;
                        page-break-after: auto;
                    }

                    header {
                        display: flex;
                        justify-content: space-between;
                        align-items: center;
                        border-bottom: 2px solid #111;
                        padding-bottom: 5mm;
                    }

                    h1 {
                        font-size: 20px;
                        margin: 0;
                    }

                    h2 {
                        font-size: 23px;
                        margin: 3mm 0 0;
                        overflow-wrap: anywhere;
                    }

                    small {
                        display: block;
                        font-size: 10px;
                        letter-spacing: 1px;
                        margin-bottom: 2mm;
                    }

                    .campo {
                        display: flex;
                        flex-direction: column;
                        gap: 2mm;
                    }

                    .campo strong {
                        font-size: 16px;
                        overflow-wrap: anywhere;
                    }

                    .identificacion {
                        border: 2px solid #111;
                        padding: 3mm;
                        display: flex;
                        align-items: center;
                        gap: 4mm;
                    }

                    .identificacion svg {
                        flex: none;
                    }

                    footer {
                        margin-top: auto;
                        border-top: 1px solid #111;
                        padding-top: 4mm;
                        display: flex;
                        justify-content: space-between;
                        font-size: 12px;
                        font-weight: bold;
                    }

                    @media screen {
                        body {
                            background: #eee;
                        }

                        .etiqueta {
                            margin: 12px auto;
                            background: white;
                            box-shadow: 0 2px 12px #ccc;
                        }
                    }
                </style>
            </head>
            <body>${etiquetas}</body>
            </html>
        `);
        ventana.document.close();
        ventana.focus();

        // El usuario imprime desde la ventana nueva
        // con Ctrl+P o guarda como PDF.
    }
};
