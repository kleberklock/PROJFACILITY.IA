using System.Linq;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Pinecone;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json; 
using System.Collections.Generic; 

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
            string? activeContexts, // <--- ALTERADO PARA string? (OPCIONAL)
            CancellationToken ct) 
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return ("Erro: Usuário não encontrado.", 0);

            if (user.LastResetDate < DateTime.UtcNow.AddMonths(-1))
            {
                user.UsedTokensCurrentMonth = 0;
                user.LastResetDate = DateTime.UtcNow;
            }

            int limiteAtual = user.Plan == "Pro" ? LIMIT_PRO : user.Plan == "Enterprise" ? int.MaxValue : LIMIT_FREE;
            if (user.UsedTokensCurrentMonth >= limiteAtual)
                return ($"Limite do plano {user.Plan} atingido.", 0);

            var agent = await _context.Agents.FirstOrDefaultAsync(a => a.Name == agentId, ct);
            string systemInstruction = agent?.SystemInstruction ?? "Você é um assistente virtual útil.";

            // --- LÓGICA DE CONTEXTOS ATIVOS ---
            // Verifica se activeContexts não é nulo nem vazio antes de usar
            if (!string.IsNullOrEmpty(activeContexts))
            {
                systemInstruction += $"\n\n[CONTEXTOS ATIVOS DEFINIDOS PELO USUÁRIO]:\n{activeContexts}";
            }
            // ----------------------------------

            string contextoExtraido = await BuscarConhecimentoNoPinecone(userMessage, agentId, userId);

            if (!string.IsNullOrEmpty(contextoExtraido))
            {
                systemInstruction += $"\n\n[BASE DE CONHECIMENTO]:\nUse as informações a seguir para responder. Se a resposta estiver aqui, priorize-a:\n---\n{contextoExtraido}\n---\n";
            }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro OpenAI");
                return ($"Erro: {ex.Message}", 0);
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
                
                var filtroUserId = new Metadata();
                filtroUserId.Add("$in", new string[] { userId.ToString(), "system" });

                var filtroPrincipal = new Metadata();
                filtroPrincipal.Add("tag", profissao);
                filtroPrincipal.Add("userId", filtroUserId); 

                var searchRequest = new QueryRequest
                {
                    Vector = vetorPergunta,
                    TopK = 5,
                    IncludeMetadata = true,
                    Filter = filtroPrincipal
                };

                var searchResponse = await index.QueryAsync(searchRequest);
                var matches = searchResponse.Matches ?? new ScoredVector[0]; 

                if (matches.Any())
                {
                    var trechos = matches
                        .Where(m => m.Score > 0.65 && m.Metadata != null && m.Metadata.ContainsKey("text"))
                        .Select(m => m.Metadata?["text"]?.ToString() ?? "")
                        .Where(t => !string.IsNullOrWhiteSpace(t));

                    return string.Join("\n---\n", trechos);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro Pinecone Search");
            }
            return "";
        }

        private string GerarRespostaSimulada(string agentId, string userMessage) => "[MODO OFFLINE] Verifique API Keys.";

        public async Task<object> DebugRetrieval(string query, string agentName, int userId)
        {
            if (_embeddingClient == null || _pinecone == null) return new { Error = "Serviços offline" };

            try
            {
                var emb = await _embeddingClient.GenerateEmbeddingAsync(query);
                float[] vec = emb.Value.Vector.ToArray();

                var filtroUserId = new Metadata();
                filtroUserId.Add("$in", new string[] { userId.ToString(), "system" });

                var filtroPrincipal = new Metadata();
                filtroPrincipal.Add("tag", agentName);
                filtroPrincipal.Add("userId", filtroUserId);

                var index = _pinecone.Index(_indexName);
                var resp = await index.QueryAsync(new QueryRequest
                {
                    Vector = vec,
                    TopK = 5,
                    IncludeMetadata = true,
                    Filter = filtroPrincipal
                });

                var safeMatches = resp.Matches ?? new ScoredVector[0];

                return new 
                {
                    Query = query,
                    UserIdBuscado = userId,
                    AgentTagBuscada = agentName,
                    MatchesEncontrados = safeMatches.Count(), 
                    Detalhes = safeMatches.Select(m => new 
                    {
                        Score = m.Score,
                        Id = m.Id,
                        Metadata = m.Metadata
                    })
                };
            }
            catch (Exception ex)
            {
                return new { Error = ex.Message, Stack = ex.StackTrace };
            }
        }
    }
}