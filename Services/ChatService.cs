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
        private readonly PineconeClient _pinecone;
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

            if (string.IsNullOrEmpty(apiKey)) _logger.LogWarning("OpenAI:ApiKey não encontrada.");
            if (string.IsNullOrEmpty(pineconeKey)) _logger.LogWarning("Pinecone:ApiKey não encontrada.");

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

        public async Task<(string Response, int Tokens)> GetAIResponse(string userMessage, string agentId, List<PROJFACILITY.IA.Models.ChatMessage> historicoDb, int userId)
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

            // 2. BUSCA CONTEXTO (RAG) - Passando userId
            var agent = await _context.Agents.FirstOrDefaultAsync(a => a.Name == agentId);
            string systemInstruction = agent?.SystemInstruction ?? "Você é um assistente virtual útil e profissional.";

            string contextoExtraido = await BuscarConhecimentoNoPinecone(userMessage, agentId, userId);

            if (!string.IsNullOrEmpty(contextoExtraido))
            {
                systemInstruction += $"\n\nBASE DE CONHECIMENTO (Use isso para responder):\n{contextoExtraido}\n";
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

                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);
                
                if (completion.Content != null && completion.Content.Count > 0)
                {
                    int totalTokens = completion.Usage != null ? completion.Usage.TotalTokens : 0;
                    string resposta = completion.Content[0].Text;

                    user.UsedTokensCurrentMonth += totalTokens;
                    user.LastLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return (resposta, totalTokens);
                }
                return ("A IA não retornou resposta.", 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao comunicar com OpenAI para o usuário {UserId}", userId);
                return ($"Erro de comunicação com a IA: {ex.Message}", 0);
            }
        }

        // ALTERADO: Adicionado userId e filtro
        private async Task<string> BuscarConhecimentoNoPinecone(string query, string profissao, int userId)
        {
            if (_embeddingClient == null || _pinecone == null) return "";

            try
            {
                var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(query);
                float[] vetorPergunta = embeddingResult.Value.Vector.ToArray();

                var index = _pinecone.Index(_indexName);
                var searchRequest = new QueryRequest
                {
                    Vector = vetorPergunta,
                    TopK = 3,
                    IncludeMetadata = true,
                    // FILTRO DE PRIVACIDADE: Só traz dados desse userId E dessa profissão
                    Filter = new Metadata { 
                        { "profissao", profissao },
                        { "userId", userId.ToString() }
                    }
                };

                var searchResponse = await index.QueryAsync(searchRequest);

                if (searchResponse.Matches != null && searchResponse.Matches.Any())
                {
                    var trechos = searchResponse.Matches
                        .Where(m => m.Metadata != null && m.Metadata.ContainsKey("texto"))
                        .Select(m => m.Metadata?["texto"]?.ToString() ?? "")
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