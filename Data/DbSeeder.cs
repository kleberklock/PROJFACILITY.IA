using PROJFACILITY.IA.Models;
using System.Linq;

namespace PROJFACILITY.IA.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Lista completa convertida do constants.js
            var rawAgents = new[] 
            {
                new { Name = "Advogado Civil", Icon = "fa-scale-balanced", Area = "juridico" },
                new { Name = "Advogado Trabalhista", Icon = "fa-briefcase", Area = "juridico" },
                new { Name = "Advogado Criminalista", Icon = "fa-handcuffs", Area = "juridico" },
                new { Name = "Advogado Tributarista", Icon = "fa-file-invoice-dollar", Area = "juridico" },
                new { Name = "Advogado Imobiliário", Icon = "fa-house-chimney", Area = "juridico" },
                new { Name = "Advogado de Família", Icon = "fa-people-roof", Area = "juridico" },
                new { Name = "Advogado Digital/LGPD", Icon = "fa-laptop-code", Area = "juridico" },
                new { Name = "Advogado Ambiental", Icon = "fa-tree", Area = "juridico" },
                new { Name = "Advogado Corporativo", Icon = "fa-building", Area = "juridico" },
                new { Name = "Advogado Previdenciário", Icon = "fa-person-cane", Area = "juridico" },
                new { Name = "Juiz / Magistrado", Icon = "fa-gavel", Area = "juridico" },
                new { Name = "Promotor de Justiça", Icon = "fa-book-atlas", Area = "juridico" },
                new { Name = "Defensor Público", Icon = "fa-shield-halved", Area = "juridico" },
                new { Name = "Oficial de Cartório", Icon = "fa-stamp", Area = "juridico" },
                new { Name = "Paralegal", Icon = "fa-folder-open", Area = "juridico" },
                
                new { Name = "Médico Clínico Geral", Icon = "fa-user-doctor", Area = "saude" },
                new { Name = "Cardiologista", Icon = "fa-heart-pulse", Area = "saude" },
                new { Name = "Dermatologista", Icon = "fa-hand-dots", Area = "saude" },
                new { Name = "Pediatra", Icon = "fa-baby", Area = "saude" },
                new { Name = "Ortopedista", Icon = "fa-bone", Area = "saude" },
                new { Name = "Ginecologista", Icon = "fa-person-pregnant", Area = "saude" },
                new { Name = "Psiquiatra", Icon = "fa-brain", Area = "saude" },
                new { Name = "Neurologista", Icon = "fa-bolt", Area = "saude" },
                new { Name = "Cirurgião Plástico", Icon = "fa-scalpel", Area = "saude" },
                new { Name = "Oftalmologista", Icon = "fa-eye", Area = "saude" },
                new { Name = "Dentista Geral", Icon = "fa-tooth", Area = "saude" },
                new { Name = "Ortodontista", Icon = "fa-teeth", Area = "saude" },
                new { Name = "Implantodontista", Icon = "fa-screwdriver", Area = "saude" },
                new { Name = "Psicólogo Clínico", Icon = "fa-comments", Area = "saude" },
                new { Name = "Nutricionista", Icon = "fa-apple-whole", Area = "saude" },
                new { Name = "Fisioterapeuta", Icon = "fa-person-walking", Area = "saude" },
                new { Name = "Enfermeiro(a)", Icon = "fa-user-nurse", Area = "saude" },
                new { Name = "Farmacêutico", Icon = "fa-pills", Area = "saude" },
                new { Name = "Médico Veterinário", Icon = "fa-dog", Area = "saude" },
                new { Name = "Personal Trainer", Icon = "fa-dumbbell", Area = "saude" },
                
                new { Name = "Dev. Front-End", Icon = "fa-desktop", Area = "tech" },
                new { Name = "Dev. Back-End", Icon = "fa-server", Area = "tech" },
                new { Name = "Dev. FullStack", Icon = "fa-layer-group", Area = "tech" },
                new { Name = "Dev. Mobile (iOS)", Icon = "fa-apple", Area = "tech" },
                new { Name = "Dev. Mobile (Android)", Icon = "fa-android", Area = "tech" },
                new { Name = "DevOps Engineer", Icon = "fa-infinity", Area = "tech" },
                new { Name = "Engenheiro de Dados", Icon = "fa-database", Area = "tech" },
                new { Name = "Cientista de Dados", Icon = "fa-chart-pie", Area = "tech" },
                new { Name = "Analista de Segurança", Icon = "fa-user-secret", Area = "tech" },
                new { Name = "Arquiteto de Software", Icon = "fa-sitemap", Area = "tech" },
                new { Name = "QA / Tester", Icon = "fa-bug-slash", Area = "tech" },
                new { Name = "Product Manager (PM)", Icon = "fa-clipboard-check", Area = "tech" },
                new { Name = "Scrum Master", Icon = "fa-rotate", Area = "tech" },
                new { Name = "Dev. Blockchain", Icon = "fa-bitcoin-sign", Area = "tech" },
                new { Name = "Dev. de Jogos", Icon = "fa-gamepad", Area = "tech" },
                new { Name = "Engenheiro de IA", Icon = "fa-robot", Area = "tech" },
                new { Name = "UX Designer", Icon = "fa-pen-nib", Area = "tech" },
                new { Name = "UI Designer", Icon = "fa-palette", Area = "tech" },
                new { Name = "Admin de Redes", Icon = "fa-network-wired", Area = "tech" },
                new { Name = "Suporte Técnico", Icon = "fa-headset", Area = "tech" },
                
                new { Name = "Engenheiro Civil", Icon = "fa-helmet-safety", Area = "engenharia" },
                new { Name = "Engenheiro Elétrico", Icon = "fa-bolt", Area = "engenharia" },
                new { Name = "Engenheiro Mecânico", Icon = "fa-gears", Area = "engenharia" },
                new { Name = "Engenheiro de Produção", Icon = "fa-industry", Area = "engenharia" },
                new { Name = "Engenheiro Químico", Icon = "fa-flask", Area = "engenharia" },
                new { Name = "Engenheiro Agrônomo", Icon = "fa-wheat-awn", Area = "engenharia" },
                new { Name = "Engenheiro Ambiental", Icon = "fa-leaf", Area = "engenharia" },
                new { Name = "Eng. de Segurança", Icon = "fa-shield", Area = "engenharia" },
                new { Name = "Arquiteto Urbanista", Icon = "fa-city", Area = "engenharia" },
                new { Name = "Designer de Interiores", Icon = "fa-couch", Area = "engenharia" },
                new { Name = "Paisagista", Icon = "fa-tree-city", Area = "engenharia" },
                new { Name = "Topógrafo", Icon = "fa-mountain-sun", Area = "engenharia" },
                
                new { Name = "Contador", Icon = "fa-calculator", Area = "negocios" },
                new { Name = "Auditor Fiscal", Icon = "fa-magnifying-glass-dollar", Area = "negocios" },
                new { Name = "Analista Financeiro", Icon = "fa-chart-line", Area = "negocios" },
                new { Name = "Consultor de Investimentos", Icon = "fa-sack-dollar", Area = "negocios" },
                new { Name = "Trader", Icon = "fa-arrow-trend-up", Area = "negocios" },
                new { Name = "Economista", Icon = "fa-money-bill-transfer", Area = "negocios" },
                new { Name = "Gestor de RH", Icon = "fa-users", Area = "negocios" },
                new { Name = "Recrutador / Headhunter", Icon = "fa-user-plus", Area = "negocios" },
                new { Name = "Business Partner", Icon = "fa-handshake", Area = "negocios" },
                new { Name = "Gerente de Projetos", Icon = "fa-timeline", Area = "negocios" },
                new { Name = "Corretor de Imóveis", Icon = "fa-house", Area = "negocios" },
                new { Name = "Corretor de Seguros", Icon = "fa-file-shield", Area = "negocios" },
                new { Name = "Consultor Empresarial", Icon = "fa-briefcase", Area = "negocios" },
                new { Name = "CEO / Executivo", Icon = "fa-user-tie", Area = "negocios" },
                new { Name = "Vendedor B2B", Icon = "fa-phone", Area = "negocios" },
                new { Name = "Representante Comercial", Icon = "fa-suitcase", Area = "negocios" },
                
                new { Name = "Gestor de Logística", Icon = "fa-truck-fast", Area = "operacional" },
                new { Name = "Comprador / Supply", Icon = "fa-cart-shopping", Area = "operacional" },
                new { Name = "Chef de Cozinha", Icon = "fa-utensils", Area = "operacional" },
                new { Name = "Nutricionista Gastronômico", Icon = "fa-carrot", Area = "operacional" },
                new { Name = "Organizador de Eventos", Icon = "fa-champagne-glasses", Area = "operacional" },
                new { Name = "Agente de Viagens", Icon = "fa-plane", Area = "operacional" },
                new { Name = "Piloto de Avião", Icon = "fa-plane-up", Area = "operacional" },
                new { Name = "Policial / Segurança", Icon = "fa-shield-cat", Area = "operacional" },
                new { Name = "Bombeiro Civil", Icon = "fa-fire-extinguisher", Area = "operacional" },
                new { Name = "Síndico Profissional", Icon = "fa-building-user", Area = "operacional" },

                new { Name = "Copywriter", Icon = "fa-feather", Area = "criativos" },
                new { Name = "Redator SEO", Icon = "fa-keyboard", Area = "criativos" },
                new { Name = "Social Media Manager", Icon = "fa-hashtag", Area = "criativos" },
                new { Name = "Gestor de Tráfego", Icon = "fa-bullhorn", Area = "criativos" },
                new { Name = "Designer Gráfico", Icon = "fa-pen-ruler", Area = "criativos" },
                new { Name = "Diretor de Arte", Icon = "fa-palette", Area = "criativos" },
                new { Name = "Editor de Vídeo", Icon = "fa-film", Area = "criativos" },
                new { Name = "Motion Designer", Icon = "fa-play", Area = "criativos" },
                new { Name = "Fotógrafo", Icon = "fa-camera", Area = "criativos" },
                new { Name = "Jornalista", Icon = "fa-newspaper", Area = "criativos" },
                new { Name = "Roteirista", Icon = "fa-scroll", Area = "criativos" },
                new { Name = "Tradutor", Icon = "fa-language", Area = "criativos" },
                new { Name = "Revisor de Texto", Icon = "fa-glasses", Area = "criativos" },
                new { Name = "Produtor Musical", Icon = "fa-music", Area = "criativos" },
                new { Name = "Influenciador Digital", Icon = "fa-star", Area = "criativos" },
                
                new { Name = "Professor Fundamental", Icon = "fa-chalkboard-user", Area = "educacao" },
                new { Name = "Professor Universitário", Icon = "fa-graduation-cap", Area = "educacao" },
                new { Name = "Pesquisador Acadêmico", Icon = "fa-microscope", Area = "educacao" },
                new { Name = "Pedagogo", Icon = "fa-shapes", Area = "educacao" },
                new { Name = "Professor de Idiomas", Icon = "fa-earth-americas", Area = "educacao" },
                new { Name = "Orientador Vocacional", Icon = "fa-compass", Area = "educacao" },
                new { Name = "Bibliotecário", Icon = "fa-book", Area = "educacao" },
                new { Name = "Estudante", Icon = "fa-backpack", Area = "educacao" }
            };

            // Loop Inteligente de Criação/Atualização
            foreach (var template in rawAgents)
            {
                var existing = context.Agents.FirstOrDefault(a => a.Name == template.Name);

                // Gera o Prompt automaticamente
                string generatedPrompt = GetSystemInstruction(template.Name, template.Area);

                if (existing == null)
                {
                    // Se não existe, cria novo
                    context.Agents.Add(new Agent
                    {
                        Name = template.Name,
                        Icon = template.Icon,
                        Specialty = template.Area,
                        Description = $"Assistente especializado em {template.Name}.",
                        SystemInstruction = generatedPrompt,
                        IsPublic = true,
                        UserId = null // Agente do Sistema
                    });
                }
                else
                {
                    // Se existe e é do sistema, atualiza o prompt (caso tenha mudado a lógica)
                    if (existing.UserId == null)
                    {
                        existing.SystemInstruction = generatedPrompt;
                        existing.Icon = template.Icon;
                        existing.Specialty = template.Area;
                    }
                }
            }

            context.SaveChanges();
        }

        // --- GERADOR DE PROMPTS DINÂMICO ---
        private static string GetSystemInstruction(string name, string area)
        {
            string basePrompt = $"Você é um {name} altamente qualificado e experiente (IA Especialista). ";

            switch (area.ToLower())
            {
                case "juridico":
                    return basePrompt + "Sua especialidade é a legislação brasileira (CF, CC, CLT, etc). Seu tom deve ser formal, preciso e analítico. Ajude a redigir contratos, analisar cláusulas e explicar leis. ALERTA OBRIGATÓRIO: Sempre inicie ou finalize avisando que você é uma IA e suas respostas não substituem uma consulta oficial com um advogado e não têm valor legal.";

                case "saude":
                    return basePrompt + "Sua missão é explicar termos médicos, orientar sobre bem-estar e saúde preventiva. Use linguagem empática e clara. ALERTA CRÍTICO: NUNCA dê diagnósticos definitivos, nunca receite medicamentos controlados e sempre recomende que o usuário procure um médico ou hospital em caso de sintomas.";

                case "tech":
                    return basePrompt + "Você é um especialista em tecnologia. Ao fornecer código, priorize Clean Code, padrões de projeto (SOLID, Design Patterns) e segurança. Explique o 'porquê' de cada solução. Se for corrigir um bug, explique a causa raiz.";

                case "engenharia":
                    return basePrompt + "Você atua com precisão técnica, normas (ABNT/ISO) e cálculos. Priorize a segurança, eficiência e sustentabilidade nos projetos. Use a terminologia técnica correta da sua área de engenharia.";

                case "negocios":
                    return basePrompt + "Você é um estrategista corporativo. Foque em ROI (Retorno sobre Investimento), KPIs, crescimento escalável e eficiência. Suas respostas devem ser orientadas a resultados e business intelligence.";

                case "criativos":
                    return basePrompt + "Você é criativo, inovador e inspirador. Ajude a gerar ideias, textos persuasivos (copywriting) e conceitos visuais. Seu tom pode ser mais descontraído e original, fugindo do óbvio.";

                case "educacao":
                    return basePrompt + "Você é um educador paciente e didático. Use analogias, exemplos práticos e divida conceitos complexos em partes simples. Verifique se o aluno entendeu e incentive o aprendizado contínuo.";

                case "operacional":
                    return basePrompt + "Você é focado em eficiência, processos e logística. Suas respostas devem ser práticas, diretas e orientadas para a resolução rápida de problemas do dia a dia.";

                default:
                    return basePrompt + "Seu objetivo é ajudar o usuário com respostas úteis, diretas e profissionais. Mantenha um tom prestativo e focado na solução do problema apresentado.";
            }
        }
    }
}