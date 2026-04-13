/* CONFIGURAÇÃO DE AMBIENTE UNIVERSAL - FACILITY.IA */

// Esta linha deteta automaticamente se o site está a rodar em localhost ou na Azure.
// Ela captura o domínio, o protocolo (http/https) e a porta de forma dinâmica.
const BASE_URL = window.location.origin; 

// Configuração para o sistema de Login (utilizado no login.html)
const CONFIG = {
    API_URL: BASE_URL
};

// Configuração para o Perfil e outras funcionalidades (utilizado no script.js)
const API_BASE_URL = BASE_URL;

console.log("Facility.IA: Conectado em", BASE_URL);