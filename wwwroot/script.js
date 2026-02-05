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
}