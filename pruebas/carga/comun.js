// Utilidades compartidas por los escenarios de carga (opcional 7.2). Ver LEEME.md.
import http from "k6/http";
import { check, fail } from "k6";

export const BASE_URL = __ENV.BASE_URL || "http://api.localhost:8000";
const COOKIE = "UltimaMilla.Auth";

const json = { headers: { "Content-Type": "application/json" } };

function cookieDe(respuesta) {
    const c = respuesta.cookies[COOKIE];
    if (!c || c.length === 0) fail(`La respuesta ${respuesta.status} de ${respuesta.url} no dejó la cookie de sesión.`);
    return c[0].value;
}

/**
 * Inicia sesión una sola vez (en setup) y devuelve la cookie: así la carga no choca con el límite de logins por IP.
 * aplicacion: "backoffice" o "movil". El repartidor elige además su operador, como hace la app.
 */
export function iniciarSesion(email, aplicacion) {
    const password = __ENV.DEMO_PASSWORD || fail("Falta DEMO_PASSWORD (la misma del .env del compose).");
    const login = http.post(`${BASE_URL}/api/auth/login`,
        JSON.stringify({ email, password, aplicacion }), json);
    check(login, { "login 200": r => r.status === 200 }) || fail(`Login de ${email}: ${login.status} ${login.body}`);

    if (aplicacion !== "movil") return cookieDe(login);

    const membresias = http.get(`${BASE_URL}/api/auth/membresias`).json();
    const repartidor = membresias.find(m => m.perfil === "Repartidor") || fail(`${email} no es repartidor.`);
    const seleccion = http.post(`${BASE_URL}/api/auth/seleccionar-operador`,
        JSON.stringify({ membresiaId: repartidor.id }), json);
    check(seleccion, { "operador elegido": r => r.status === 200 }) || fail(`Seleccionar operador: ${seleccion.status}`);
    return cookieDe(seleccion);
}

/** Cada VU usa la cookie que preparó setup. */
export function usarSesion(cookie) {
    http.cookieJar().set(BASE_URL, COOKIE, cookie);
}

export function uuid() {
    // Suficiente para Ids de prueba; no hace falta un generador criptográfico.
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, c => {
        const r = (Math.random() * 16) | 0;
        return (c === "x" ? r : (r & 0x3) | 0x8).toString(16);
    });
}

export { json };
