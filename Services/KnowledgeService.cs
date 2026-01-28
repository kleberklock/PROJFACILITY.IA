using OpenAI.Embeddings;
using Pinecone;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig;
using Tesseract;
using System.Text;
using PROJFACILITY.IA.Data; 
using PROJFACILITY.IA.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PROJFACILITY.IA.Services
{
    public class KnowledgeService
    {
        private readonly PineconeClient? _pinecone;
        private readonly EmbeddingClient? _embeddingClient;
        private readonly AppDbContext _context;
        private readonly ILogger<KnowledgeService> _logger;
        
        private readonly string _indexName = "facility-ia"; 

        public KnowledgeService(IConfiguration config, AppDbContext context, ILogger<KnowledgeService> logger)
        {
            _context = context;
            _logger = logger;

            string pineconeKey = config["Pinecone:ApiKey"] ?? "";
            string openAiKey = config["OpenAI:ApiKey"] ?? "";

            if (!string.IsNullOrEmpty(pineconeKey))
            {
                try { _pinecone = new PineconeClient(pineconeKey); }
                catch (Exception ex) { _logger.LogError(ex, "Erro Pinecone"); }
            }
            
            if (!string.IsNullOrEmpty(openAiKey))
            {
                try { _embeddingClient = new EmbeddingClient("text-embedding-3-small", openAiKey); }
                catch (Exception ex) { _logger.LogError(ex, "Erro OpenAI"); }
            }
        }

        // ========================================================================================
        // ÁREA DE COMPATIBILIDADE (CORREÇÃO DOS ERROS DE COMPILAÇÃO)
        // ========================================================================================

        // CORREÇÃO 1: Sobrecarga para aceitar STRING (Caminho do arquivo) como o Controller antigo faz
        public async Task IngerirConhecimento(string caminhoArquivo, string nome, string profissao, int userId)
        {
            // Abre o arquivo do disco como Stream e manda processar
            using (var stream = File.OpenRead(caminhoArquivo))
            {
                await ProcessarArquivoEIngerir(stream, nome, profissao, userId);
            }
        }

        // Mantém a versão com Stream caso algum outro lugar use
        public Task IngerirConhecimento(Stream stream, string nome, string profissao, int userId)
        {
            return ProcessarArquivoEIngerir(stream, nome, profissao, userId);
        }

        // CORREÇÃO 2: Sobrecarga para o AdminController (que só manda o ID do arquivo, sem UserID)
        public async Task<bool> ExcluirArquivo(int id)
        {
            // Admin pode excluir qualquer arquivo, então buscamos apenas pelo ID
            return await ExcluirDocumentoAdmin(id);
        }

        public Task<bool> ExcluirArquivo(int id, int userId)
        {
            return ExcluirDocumento(id, userId);
        }
        
        // ========================================================================================
        // LÓGICA PRINCIPAL
        // ========================================================================================

        public async Task ProcessarArquivoEIngerir(Stream arquivoStream, string nomeArquivo, string profissao, int userId)
        {
            if (_pinecone == null || _embeddingClient == null) throw new Exception("IA Services offline");

            try 
            {
                string extensao = Path.GetExtension(nomeArquivo).ToLower();
                string textoExtraido = "";

                if (extensao == ".pdf") textoExtraido = ExtrairTextoPDF(arquivoStream);
                else if (extensao == ".jpg" || extensao == ".png") textoExtraido = ExtrairTextoImagem(arquivoStream);
                else 
                {
                    using (var reader = new StreamReader(arquivoStream))
                    {
                        if(arquivoStream.CanSeek) arquivoStream.Position = 0;
                        textoExtraido = await reader.ReadToEndAsync();
                    }
                }

                if (string.IsNullOrWhiteSpace(textoExtraido)) return;

                var doc = new KnowledgeDocument
                {
                    UserId = userId,
                    FileName = nomeArquivo,
                    FileType = extensao,
                    VectorIdPrefix = $"{userId}_{Guid.NewGuid()}",
                    CreatedAt = DateTime.UtcNow,
                    Tag = profissao
                };
                _context.KnowledgeDocuments.Add(doc);
                await _context.SaveChangesAsync();

                var chunks = QuebrarTexto(textoExtraido, 1000);
                var vectors = new List<Vector>();
                var index = _pinecone.Index(_indexName);

                int chunkIndex = 0;
                foreach (var chunk in chunks)
                {
                    var embedding = await _embeddingClient.GenerateEmbeddingAsync(chunk);
                    float[] vetor = embedding.Value.Vector.ToArray();

                    var metadata = new Metadata
                    {
                        { "text", chunk }, 
                        { "tag", profissao }, 
                        { "userId", userId.ToString() },
                        { "filename", nomeArquivo },
                        { "docId", doc.Id.ToString() }
                    };

                    vectors.Add(new Vector
                    {
                        Id = $"{doc.VectorIdPrefix}_{chunkIndex}",
                        Values = vetor,
                        Metadata = metadata
                    });
                    chunkIndex++;
                }

                await index.UpsertAsync(new UpsertRequest { Vectors = vectors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro upload");
                throw;
            }
        }

        // Exclusão Segura (Usuário só apaga o seu)
        public async Task<bool> ExcluirDocumento(int docId, int userId)
        {
            var doc = await _context.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.UserId == userId);
            if (doc == null) return false;
            return await ExecutarExclusao(doc);
        }

        // Exclusão Admin (Apaga qualquer um pelo ID)
        private async Task<bool> ExcluirDocumentoAdmin(int docId)
        {
            var doc = await _context.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == docId);
            if (doc == null) return false;
            return await ExecutarExclusao(doc);
        }

        private async Task<bool> ExecutarExclusao(KnowledgeDocument doc)
        {
            if (_pinecone == null) return false;
            try
            {
                var index = _pinecone.Index(_indexName);
                var filter = new Metadata { { "docId", doc.Id.ToString() } };
                await index.DeleteAsync(new DeleteRequest { Filter = filter });

                _context.KnowledgeDocuments.Remove(doc);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro delete");
                return false;
            }
        }

        // --- Métodos Auxiliares ---

        private string ExtrairTextoPDF(Stream stream)
        {
            var sb = new StringBuilder();
            try {
                if(stream.CanSeek) stream.Position = 0;
                using (var pdf = PdfDocument.Open(stream))
                    foreach (var page in pdf.GetPages()) sb.AppendLine(page.Text);
            } catch { }
            return sb.ToString();
        }

        private string ExtrairTextoImagem(Stream stream)
        {
            try {
                if(stream.CanSeek) stream.Position = 0;
                string tessPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
                using var engine = new TesseractEngine(tessPath, "por", EngineMode.Default);
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                using var pix = Pix.LoadFromMemory(ms.ToArray());
                using var page = engine.Process(pix);
                return page.GetText();
            } catch { return ""; }
        }

        private List<string> QuebrarTexto(string texto, int tamanhoMax) {
            var lista = new List<string>();
            if (string.IsNullOrEmpty(texto)) return lista;
            texto = texto.Replace("\r", " ").Replace("\n", " ");
            for (int i = 0; i < texto.Length; i += tamanhoMax) {
                int tamanho = Math.Min(tamanhoMax, texto.Length - i);
                lista.Add(texto.Substring(i, tamanho));
            }
            return lista;
        }
    }
}