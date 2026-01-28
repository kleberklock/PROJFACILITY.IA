using System.Linq; // <--- ADICIONADO (Essencial para .Any(), .Where(), .Select())
using OpenAI.Chat;
using OpenAI.Embeddings;
using Pinecone;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using Microsoft.Extensions.Logging;

namespace PROJFACILITY.IA.Services
{
    public class ChatService
    {
        private readonly ChatClient? _chatClient;
        private readonly EmbeddingClient? _embeddingClient;
        private readonly PineconeClient? _pinecone;
        private readonly AppDbContext _context;
        private readonly ILogger<ChatService> _logger;
        private readonly string _indexName = "facility-ia";

        // Limites de Plano
        private const int LIMIT_FREE = 5000; 
        private const int LIMIT_PRO = 100000;

        public ChatService(IConfiguration configuration, AppDbContext context, ILogger<ChatService> logger)
        {
            _context = context;
            _logger = logger;

            var apiKey = configuration["OpenAI:ApiKey"];
            var pineconeKey = configuration["Pinecone:ApiKey"] ?? "";

            if (!string.IsNullOrEmpty(pineconeKey))
            {
                try { _pinecone = new PineconeClient(pineconeKey); }
                catch (Exception ex) { _logger.LogError(ex, "Erro ao iniciar Pinecone"); }
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    _chatClient = new ChatClient("gpt-4o-mini", apiKey);
                    _embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
                }
                catch (Exception ex) { _logger.LogError(ex, "Erro ao iniciar OpenAI"); }
            }
        }

        public async Task<(string Response, int Tokens)> GetAIResponse(
            string userMessage, 
            string agentId, 
            List<PROJFACILITY.IA.Models.ChatMessage> historicoDb, 
            int userId,
            CancellationToken ct) 
        {
            // 1. VERIFICA PLANO
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return ("Erro: Usuário não encontrado.", 0);

            if (user.LastResetDate < DateTime.UtcNow.AddMonths(-1))
            {
                user.UsedTokensCurrentMonth = 0;
                user.LastResetDate = DateTime.UtcNow;
            }

            int limiteAtual = user.Plan == "Pro" ? LIMIT_PRO : user.Plan == "Enterprise" ? int.MaxValue : LIMIT_FREE;

            if (user.UsedTokensCurrentMonth >= limiteAtual)
            {
                return ($"Limite do plano {user.Plan} atingido. Faça upgrade para continuar.", 0);
            }

            // 2. BUSCA CONTEXTO (RAG)
            var agent = await _context.Agents.FirstOrDefaultAsync(a => a.Name == agentId, ct);
            string systemInstruction = agent?.SystemInstruction ?? "Você é um assistente virtual útil e profissional.";

            // --- RAG COM LOGS ---
            string contextoExtraido = await BuscarConhecimentoNoPinecone(userMessage, agentId, userId);

            if (!string.IsNullOrEmpty(contextoExtraido))
            {
                // Adiciona o contexto ao prompt do sistema
                systemInstruction += $"\n\n[BASE DE CONHECIMENTO - INFORMAÇÕES EXTRAS]:\nUse as informações abaixo para responder à pergunta do usuário. Se a resposta estiver aqui, use-a. Se não, use seu conhecimento geral.\n---\n{contextoExtraido}\n---\n";
            }
            // --------------------

            if (_chatClient == null) return (GerarRespostaSimulada(agentId, userMessage), 0);

            try
            {
                List<OpenAI.Chat.ChatMessage> messages = new() { new SystemChatMessage(systemInstruction) };
                
                foreach (var msg in historicoDb)
                {
                    if (msg.Sender == "user") messages.Add(new UserChatMessage(msg.Text));
                    else messages.Add(new AssistantChatMessage(msg.Text));
                }

                messages.Add(new UserChatMessage(userMessage));

                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, null, ct);
                
                if (completion.Content != null && completion.Content.Count > 0)
                {
                    int totalTokens = completion.Usage != null ? completion.Usage.TotalTokens : 0;
                    string resposta = completion.Content[0].Text;

                    user.UsedTokensCurrentMonth += totalTokens;
                    user.LastLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync(ct);

                    return (resposta, totalTokens);
                }
                return ("A IA não retornou resposta.", 0);
            }
            catch (OperationCanceledException)
            {
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro OpenAI");
                return ($"Erro de comunicação com a IA: {ex.Message}", 0);
            }
        }

        private async Task<string> BuscarConhecimentoNoPinecone(string query, string profissao, int userId)
        {
            if (_embeddingClient == null || _pinecone == null) return "";

            try
            {
                var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(query);
                float[] vetorPergunta = embeddingResult.Value.Vector.ToArray();

                var index = _pinecone.Index(_indexName);
                
                // CORREÇÃO: Filtro $in explícito
                var filtroUserId = new Metadata();
                filtroUserId.Add("$in", new List<string> { userId.ToString(), "system" });

                var filtroPrincipal = new Metadata();
                filtroPrincipal.Add("tag", profissao);
                filtroPrincipal.Add("userId", filtroUserId); 

                var searchRequest = new QueryRequest
                {
                    Vector = vetorPergunta,
                    TopK = 4,
                    IncludeMetadata = true,
                    Filter = filtroPrincipal
                };

                var searchResponse = await index.QueryAsync(searchRequest);

                if (searchResponse.Matches != null && searchResponse.Matches.Any())
                {
                    // Filtra apenas matches com relevância > 0.70
                    // O erro CS1503 geralmente ocorre aqui se faltar os parênteses no ToString()
                    var trechos = searchResponse.Matches
                        .Where(m => m.Score > 0.70 && m.Metadata != null && m.Metadata.ContainsKey("text"))
                        .Select(m => m.Metadata?["text"]?.ToString() ?? "") // Garante ToString()
                        .Where(t => !string.IsNullOrEmpty(t));

                    return string.Join("\n---\n", trechos);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar no Pinecone index {IndexName}", _indexName);
            }
            return "";
        }

        private string GerarRespostaSimulada(string agentId, string userMessage)
        {
             return $"[MODO OFFLINE] A IA não está respondendo. Verifique a Chave da API.";
        }
    }
}