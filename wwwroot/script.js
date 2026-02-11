/* FACILITY.IA - Core Script Limpo */

document.addEventListener('DOMContentLoaded', function() {
    // 1. Inicializa Partículas
    initParticles();
    
    // 2. Verifica Login e Carrega Dados (Foto/Nome)
    verificarLogin();
    
    // 3. Animações
    runPageAnimations();

    // 4. Lógica Mobile (Apenas correções de altura e gestos)
    if (window.innerWidth <= 992) {
        fixMobileVh();
        initMobileSystem(); // Sistema de Swipe/Toque
    }
});

/* --- SISTEMA DE ANIMAÇÃO --- */
function runPageAnimations() {
    const elements = document.querySelectorAll('.app-card, .hero-content, .login-card, .pro-card, .table-row');
    elements.forEach((el, index) => {
        el.style.opacity = '0';
        el.classList.add('animate-slide-up');
        el.style.animationDelay = `${index * 0.08}s`;
    });
    
    // Inputs Focus Effect
    document.querySelectorAll('input, textarea').forEach(input => {
        input.addEventListener('focus', () => input.parentElement.classList.add('focused'));
        input.addEventListener('blur', () => input.parentElement.classList.remove('focused'));
    });
}

/* --- MENU MOBILE (PÁGINAS PÚBLICAS: Index, Prompts, Planos) --- */
function togglePublicMenu() {
    const nav = document.getElementById('publicNav');
    const overlay = document.querySelector('.mobile-nav-overlay');
    
    if (nav) {
        const isActive = nav.classList.toggle('active');
        if (overlay) overlay.classList.toggle('active');
        document.body.style.overflow = isActive ? 'hidden' : '';
    }
}

/* --- MENU MOBILE (INTERNO: Dashboard, Chat) + GESTOS --- */
function initMobileSystem() {
    const sidebar = document.getElementById('sidebar') || document.getElementById('mainSidebar');
    const trigger = document.querySelector('.menu-trigger'); // Ícone interno
    const overlay = document.querySelector('.overlay'); // Overlay interno

    // Se não achar elementos internos, para (estamos na Home pública)
    if (!sidebar) return; 

    const closeMenu = () => {
        sidebar.classList.remove('active');
        if(overlay) overlay.classList.remove('active');
        document.body.style.overflow = '';
    };

    const openMenu = () => {
        sidebar.classList.add('active');
        if(overlay) overlay.classList.add('active');
        document.body.style.overflow = 'hidden';
    };

    // Click no botão interno
    if(trigger) {
        trigger.onclick = (e) => {
            e.stopPropagation();
            sidebar.classList.contains('active') ? closeMenu() : openMenu();
        };
    }
    
    // Click no overlay fecha
    if(overlay) overlay.onclick = closeMenu;

    // Links fecham o menu
    sidebar.querySelectorAll('a').forEach(link => link.addEventListener('click', closeMenu));

    // Gestos (Swipe)
    let touchStartX = 0;
    document.addEventListener('touchstart', e => touchStartX = e.changedTouches[0].screenX, {passive: true});
    document.addEventListener('touchend', e => {
        if (touchStartX - e.changedTouches[0].screenX > 50) closeMenu(); // Deslizar para esquerda fecha
    }, {passive: true});
}

/* --- PARTICULAS --- */
function initParticles() {
    if (document.getElementById('particles-js') && typeof particlesJS !== 'undefined') {
        particlesJS("particles-js", {
            "particles": { "number": { "value": 30 }, "color": { "value": "#00ff88" }, "opacity": { "value": 0.2 }, "size": { "value": 3 }, "move": { "enable": true, "speed": 1 } },
            "interactivity": { "events": { "onhover": { "enable": true, "mode": "grab" } } }
        });
    }
}

/* --- UTILITÁRIOS E AUTENTICAÇÃO --- */

async function verificarLogin() {
    const token = localStorage.getItem('token');
    const path = window.location.pathname.toLowerCase();
    const publicPages = ['login.html', 'index.html', 'cadastro', '/', 'prompts.html', 'planos.html'];
    const isPublic = publicPages.some(p => path.includes(p) || path === '/' || path.endsWith('/'));

    if (!token) {
        if (!isPublic) window.location.href = 'login.html';
        return;
    }

    // Se tem token, busca dados atualizados (Foto, Nome)
    try {
        // Assume que API_BASE_URL está no config.js ou define padrão
        const baseUrl = (typeof API_BASE_URL !== 'undefined') ? API_BASE_URL : 'http://localhost:5217';
        
        const response = await fetch(`${baseUrl}/api/user/profile`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const user = await response.json();
            
            // Atualiza nome e foto globalmente onde existirem esses IDs
            const nomeDisplay = document.getElementById('nomeUsuarioDisplay');
            if (nomeDisplay) nomeDisplay.textContent = user.name;

            const imgPerfil = document.getElementById('imgPerfilUsuario');
            if (imgPerfil) {
                if (user.profilePicture) {
                    imgPerfil.src = `${baseUrl}${user.profilePicture}`;
                } else {
                    imgPerfil.src = 'assets/default-avatar.png';
                }
            }
        } else {
            // Token inválido? Logout se não for público
            if (!isPublic) {
                console.warn('Sessão expirada');
                // fazerLogout(); // Descomente para forçar logout
            }
        }
    } catch (error) {
        console.error("Erro ao validar sessão:", error);
    }
}

function fixMobileVh() {
    let vh = window.innerHeight * 0.01;
    document.documentElement.style.setProperty('--vh', `${vh}px`);
    window.addEventListener('resize', () => {
        let vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);
    });
}

window.fazerLogout = function() {
    localStorage.removeItem('token');
    localStorage.removeItem('userData');
    window.location.href = 'index.html';
}

/* --- TOAST NOTIFICATIONS --- */
window.showToast = function(msg, type = 'success') {
    if (typeof Swal !== 'undefined') {
        const Toast = Swal.mixin({
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 3000,
            timerProgressBar: true,
            background: '#1a1a1a',
            color: '#ffffff',
            didOpen: (toast) => {
                toast.addEventListener('mouseenter', Swal.stopTimer)
                toast.addEventListener('mouseleave', Swal.resumeTimer)
            }
        });

        Toast.fire({
            icon: type,
            title: msg
        });
    } else {
        console.warn("SweetAlert2 não carregado. Usando alert nativo.");
        alert(msg); 
    }
}
function getCategoryColor(area) {
    // Normaliza para garantir (remove espaços e poe minusculo)
    const key = (area || 'outros').toLowerCase().trim();

    // Mapeia tanto as chaves do Backend quanto as do Frontend
    const colors = {
        'tecnologia': '#00F3FF', // Ciano Neon
        'tech': '#00F3FF',       
        
        'juridico': '#FFD700',   // Dourado
        
        'saude': '#00FF88',      // Verde Neon
        
        'engenharia': '#FF9900', // Laranja
        
        'criativos': '#FF3366',  // Rosa Neon
        'criativo': '#FF3366',
        
        'negocios': '#0099FF',   // Azul
        
        'educacao': '#9933FF',   // Roxo
        
        'operacional': '#888888',// Cinza
        
        'meus': '#7c3aed',       // Roxo do tema (Agentes Pessoais)
        'outros': '#FFFFFF'      // Branco
    };

    return colors[key] || colors['outros'];
    /* --- LÓGICA DO MODAL DE PROMPTS (SISTEMA E USUÁRIO) --- */

let cachedSystemPrompts = null;
let cachedUserPrompts = null;

// Função chamada ao clicar no ícone da caneta
function abrirMeusPromptsChat() {
    // Esconde outros menus se estiverem abertos
    const attachMenu = document.getElementById('attachMenu');
    if(attachMenu) attachMenu.style.display = 'none';
    
    // Abre modal
    const modal = document.getElementById('modalPromptsChat');
    if(modal) {
        modal.style.display = 'flex';
        // Inicia na aba "Sistema" por padrão
        switchModalTab('system');
    }
}

function fecharModalPromptsChat() {
    const modal = document.getElementById('modalPromptsChat');
    if(modal) modal.style.display = 'none';
}

function switchModalTab(tabName) {
    // Gerencia estado visual dos botões
    document.getElementById('tabBtnSystem').classList.remove('active');
    document.getElementById('tabBtnUser').classList.remove('active');
    
    // Gerencia visibilidade do conteúdo
    document.getElementById('tabContentSystem').style.display = 'none';
    document.getElementById('tabContentUser').style.display = 'none';

    if (tabName === 'system') {
        document.getElementById('tabBtnSystem').classList.add('active');
        document.getElementById('tabContentSystem').style.display = 'block';
        loadSystemPrompts();
    } else {
        document.getElementById('tabBtnUser').classList.add('active');
        document.getElementById('tabContentUser').style.display = 'block';
        loadUserPrompts();
    }
}

// --- CARREGAR PROMPTS DO SISTEMA ---
async function loadSystemPrompts() {
    const container = document.getElementById('systemPromptsContainer');
    const loader = document.getElementById('systemPromptsLoader');
    
    // Se já temos cache, usa ele
    if (cachedSystemPrompts) {
        renderSystemPrompts(cachedSystemPrompts);
        loader.style.display = 'none';
        return;
    }

    loader.style.display = 'block';
    container.innerHTML = '';

    try {
        const token = localStorage.getItem('token');
        const apiUrl = (typeof CONFIG !== 'undefined' && CONFIG.API_URL) ? CONFIG.API_URL : '';
        
        const res = await fetch(`${apiUrl}/api/prompts/system`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (res.ok) {
            cachedSystemPrompts = await res.json();
            renderSystemPrompts(cachedSystemPrompts);
        } else {
            container.innerHTML = '<div style="color:#ff6b6b; padding:20px; text-align:center;">Erro ao carregar prompts do sistema.</div>';
        }
    } catch (e) {
        console.error(e);
        container.innerHTML = '<div style="color:#ff6b6b; padding:20px; text-align:center;">Erro de conexão.</div>';
    } finally {
        loader.style.display = 'none';
    }
}

function renderSystemPrompts(prompts) {
    const container = document.getElementById('systemPromptsContainer');
    container.innerHTML = '';
    
    if(!prompts || prompts.length === 0) {
        container.innerHTML = '<p style="padding:20px; color:#999;">Nenhum prompt de sistema encontrado.</p>';
        return;
    }

    // Agrupar por Area
    const groups = {};
    prompts.forEach(p => {
        if (!groups[p.area]) groups[p.area] = [];
        groups[p.area].push(p);
    });

    // Renderizar Accordions
    for (const [area, items] of Object.entries(groups)) {
        // Criar Accordion Item
        const accItem = document.createElement('div');
        accItem.className = 'accordion-item';
        
        // Header (Área)
        const header = document.createElement('div');
        header.className = 'accordion-header';
        
        // Ícone baseado na área (opcional, simples mapeamento de cor)
        let iconColor = '#10b981'; // Padrão verde
        if(area.includes('Juridico')) iconColor = '#eab308'; // Amarelo
        if(area.includes('Tech')) iconColor = '#3b82f6'; // Azul
        if(area.includes('Saude')) iconColor = '#ef4444'; // Vermelho
        
        header.innerHTML = `
            <span style="display:flex; align-items:center;">
                <i class="fas fa-layer-group" style="margin-right:10px; color:${iconColor}"></i> 
                ${area}
            </span> 
            <i class="fas fa-chevron-down" style="transition: transform 0.3s;"></i>
        `;
        
        // Lógica de toggle do accordion
        header.onclick = () => {
            const isActive = accItem.classList.contains('active');
            // Opcional: Fechar outros accordions ao abrir um? Se quiser, descomente abaixo:
            // document.querySelectorAll('.accordion-item').forEach(i => i.classList.remove('active'));
            
            if(isActive) accItem.classList.remove('active');
            else accItem.classList.add('active');
        };
        
        // Body (Cards)
        const body = document.createElement('div');
        body.className = 'accordion-body';
        
        items.forEach(p => {
            const card = document.createElement('div');
            card.className = 'system-prompt-card';
            // Encode URI Component para evitar quebra com aspas
            card.onclick = () => selecionarPromptChat(encodeURIComponent(p.content));
            card.innerHTML = `
                <div class="sp-prof">${p.profession}</div>
                <div class="sp-title">${p.buttonTitle}</div>
            `;
            body.appendChild(card);
        });

        accItem.appendChild(header);
        accItem.appendChild(body);
        container.appendChild(accItem);
    }
}

// --- CARREGAR MEUS PROMPTS ---
async function loadUserPrompts() {
    const container = document.getElementById('listaPromptsChatContent');
    const loader = document.getElementById('userPromptsLoader');
    
    if (cachedUserPrompts) {
        renderUserPrompts(cachedUserPrompts);
        loader.style.display = 'none';
        return;
    }
    
    loader.style.display = 'block';
    container.innerHTML = '';

    try {
        const token = localStorage.getItem('token');
        const apiUrl = (typeof CONFIG !== 'undefined' && CONFIG.API_URL) ? CONFIG.API_URL : '';

        const res = await fetch(`${apiUrl}/api/prompts`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (res.ok) {
            cachedUserPrompts = await res.json();
            renderUserPrompts(cachedUserPrompts);
        } else {
            container.innerHTML = '<div style="color:#ff6b6b; padding:20px; text-align:center;">Erro ao carregar seus prompts.</div>';
        }
    } catch (e) {
        console.error(e);
        container.innerHTML = '<div style="color:#ff6b6b; padding:20px; text-align:center;">Erro de conexão.</div>';
    } finally {
        loader.style.display = 'none';
    }
}

function renderUserPrompts(prompts) {
    const container = document.getElementById('listaPromptsChatContent');
    container.innerHTML = '';

    if (!prompts || prompts.length === 0) {
        container.innerHTML = `
            <div style="text-align:center; color:#888; padding:30px;">
                <i class="fas fa-folder-open" style="font-size:2rem; margin-bottom:15px; opacity:0.5;"></i><br>
                Você não tem prompts salvos.<br>
                <a href="meus_prompts.html" style="color:var(--primary); text-decoration:none; font-size:0.9rem; margin-top:10px; display:inline-block;">Criar prompts</a>
            </div>`;
        return;
    }

    prompts.forEach(p => {
        const item = document.createElement('div');
        // Reutilizando estilo de lista, se houver, ou aplicando inline
        item.style.padding = '15px';
        item.style.borderBottom = '1px solid rgba(255,255,255,0.05)';
        item.style.cursor = 'pointer';
        item.style.transition = 'background 0.2s';
        
        item.onmouseover = () => { item.style.backgroundColor = 'rgba(255,255,255,0.03)'; };
        item.onmouseout = () => { item.style.backgroundColor = 'transparent'; };
        
        item.onclick = () => selecionarPromptChat(encodeURIComponent(p.content));
        
        item.innerHTML = `
            <strong style="color:var(--primary); font-size:1rem; display:block; margin-bottom:5px;">${p.title}</strong>
            <p style="color:#ccc; font-size:0.9rem; margin:0; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">${p.content}</p>
        `;
        container.appendChild(item);
    });
}

// Função auxiliar para inserir no chat e fechar modal
function selecionarPromptChat(conteudoEncoded) {
    const conteudo = decodeURIComponent(conteudoEncoded);
    const input = document.getElementById('userInput');
    
    if(!input) return;

    if (input.value.trim() !== '') {
        input.value += '\n\n' + conteudo;
    } else {
        input.value = conteudo;
    }
    
    // Foca e ajusta altura
    input.focus();
    input.style.height = 'auto';
    input.style.height = (input.scrollHeight) + 'px';

    fecharModalPromptsChat();
}
}