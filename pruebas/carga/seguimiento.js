// Escenario 1: seguimiento público (lo más consultado y anónimo). Rampa por tasa de llegada.
// TOKENS: lista separada por comas de tokens reales (los enlaces de los correos en Mailpit). Sin ella se
// consultan tokens inventados y se mide el camino del 404, que también pasa por la caché.
import http from "k6/http";
import { check } from "k6";
import { BASE_URL } from "./comun.js";

const tokens = (__ENV.TOKENS || "").split(",").filter(Boolean);
const tasa = Number(__ENV.TASA || 50);

http.setResponseCallback(http.expectedStatuses(200, 404));

export const options = {
    scenarios: {
        seguimiento: {
            executor: "ramping-arrival-rate",
            startRate: 1,
            timeUnit: "1s",
            preAllocatedVUs: 50,
            maxVUs: 200,
            stages: [
                { target: tasa, duration: "1m" },  // rampa
                { target: tasa, duration: "3m" },  // sostenido
                { target: 0, duration: "30s" }
            ]
        }
    },
    // Objetivos de nivel de servicio, fijados antes de correr (LEEME.md).
    thresholds: {
        http_req_failed: ["rate<0.01"],
        http_req_duration: ["p(95)<300", "p(99)<800"]
    }
};

export default function () {
    const token = tokens.length > 0
        ? tokens[Math.floor(Math.random() * tokens.length)]
        : `inexistente-${Math.floor(Math.random() * 1e9)}`;

    const r = http.get(`${BASE_URL}/api/seguimiento/${token}`, { tags: { name: "GET /api/seguimiento/{token}" } });
    check(r, { "200 o 404": x => x.status === 200 || x.status === 404 });
}
