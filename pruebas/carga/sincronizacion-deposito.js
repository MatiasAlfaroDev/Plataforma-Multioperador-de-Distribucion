// Escenario 3 (exigido por la letra): varios repartidores vuelven al depósito y sincronizan a la vez.
// Cada VU es un teléfono: sube lo que juntó sin señal como la app (lotes de 200 posiciones), informa que no le
// quedan pendientes y descarga su hoja de ruta. Todos arrancan en el mismo instante.
//
// Los eventos de entrega (eventos-movil) necesitan envíos en tránsito asignados a ese repartidor, que no se
// pueden fabricar en cada corrida sin ensuciar la base; las posiciones son la escritura más voluminosa de la
// sincronización y pasan por el mismo camino (cookie, tenant, handler, EF Core, PostgreSQL).
import http from "k6/http";
import { check } from "k6";
import { BASE_URL, iniciarSesion, usarSesion, uuid, json } from "./comun.js";

const telefonos = Number(__ENV.REPARTIDORES || 40);
const lotes = Number(__ENV.LOTES || 5);

export const options = {
    scenarios: {
        regreso_al_deposito: {
            executor: "per-vu-iterations",
            vus: telefonos,
            iterations: 1,
            maxDuration: "2m"
        }
    },
    thresholds: {
        http_req_failed: ["rate<0.01"],
        "http_req_duration{name:POST /api/repartidor/posiciones}": ["p(95)<1000", "p(99)<2000"],
        // Que todos los teléfonos terminen: si alguno no, la iteración no se completa.
        iterations: [`count==${telefonos}`]
    }
};

export function setup() {
    return { cookie: iniciarSesion(__ENV.USUARIO || "repartidor.rapi@demo.local", "movil") };
}

export default function (datos) {
    usarSesion(datos.cookie);
    // Una sincronización = un identificador de correlación, como hace la app (RNF 6.12).
    const correlacion = { "X-Correlation-Id": `k6-${__VU}-${Date.now()}` };
    const params = nombre => ({ headers: { ...json.headers, ...correlacion }, tags: { name: nombre } });

    const inicio = Date.now() - 4 * 3600 * 1000;
    for (let lote = 0; lote < lotes; lote++) {
        const posiciones = Array.from({ length: 200 }, (_, i) => ({
            id: uuid(),
            latitud: -34.9 + Math.random() * 0.1,
            longitud: -56.2 + Math.random() * 0.1,
            registradaEn: new Date(inicio + (lote * 200 + i) * 15000).toISOString()
        }));
        const r = http.post(`${BASE_URL}/api/repartidor/posiciones`, JSON.stringify({ posiciones }),
            params("POST /api/repartidor/posiciones"));
        check(r, { "lote guardado": x => x.status === 200 });
    }

    const pendientes = http.put(`${BASE_URL}/api/repartidor/sincronizacion`, JSON.stringify({ pendientes: 0 }),
        params("PUT /api/repartidor/sincronizacion"));
    check(pendientes, { "pendientes informados": x => x.status === 204 });

    const hoy = new Date().toISOString().slice(0, 10);
    const hojas = http.get(`${BASE_URL}/api/repartidor/hojas-de-ruta?fecha=${hoy}`,
        { headers: correlacion, tags: { name: "GET /api/repartidor/hojas-de-ruta" } });
    check(hojas, { "hoja descargada": x => x.status === 200 });
}
