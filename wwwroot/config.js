/* CONFIGURAÇÃO DE AMBIENTE UNIVERSAL */

// 1. Defina aqui onde o Backend está rodando (Localhost agora)
const BASE_URL = "http://localhost:5217";
//const BASE_URL = "https://facility-ia-frg6cqbcggasdhea.centralus-01.azurewebsites.net"; // Use essa só na Azure

// 2. Compatibilidade para o Login (que usa CONFIG)
const CONFIG = {
    API_URL: BASE_URL
};

 //3. Compatibilidade para o Perfil e Script Novo (que usam API_BASE_URL)
const API_BASE_URL = BASE_URL;

console.log("Facility.IA: Conectado em", BASE_URL);