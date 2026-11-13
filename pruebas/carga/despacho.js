// Escenario 2: despachadores trabajando en el Backoffice (lecturas autenticadas). Rampa de usuarios concurrentes.
import http from "k6/http";
import { check, sleep } from "k6";
import { BASE_URL, iniciarSesion, usarSesion } from "./comun.js";

const usuarios = Number(__ENV.USUARIOS || 30);

export const options = {
    scenarios: {
        despacho: {
            executor: "ramping-vus",
            startVUs: 0,
            stages: [
                { target: usuarios, duration: "1m" },
                { target: usuarios, duration: "3m" },
                { target: 0, duration: "30s" }
            ]
        }
    },
    thresholds: {
        http_req_failed: ["rate<0.01"],
        "http_req_duration{name:GET /api/envios}": ["p(95)<500", "p(99)<1000"],
        "http_req_duration{name:GET /api/tablero}": ["p(95)<500", "p(99)<1000"]
    }
};

export function setup() {
    return { cookie: iniciarSesion(__ENV.USUARIO || "despachador.rapi@demo.local", "backoffice") };
}

export default function (datos) {
    usarSesion(datos.cookie);

    const envios = http.get(`${BASE_URL}/api/envios`, { tags: { name: "GET /api/envios" } });
    check(envios, { "envíos 200": r => r.status === 200 });

    const tablero = http.get(`${BASE_URL}/api/tablero`, { tags: { name: "GET /api/tablero" } });
    check(tablero, { "tablero 200": r => r.status === 200 });

    // Un humano mira la pantalla un rato antes de volver a pedir.
    sleep(1 + Math.random() * 2);
}
