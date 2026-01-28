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
using System.IO.Compression; 
using System.Xml;

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

        // --- COMPATIBILIDADE ---
        public async Task IngerirConhecimento(string caminhoArquivo, string nome, string profissao, int userId)
        {
            using (var stream = File.OpenRead(caminhoArquivo))
            {
                await ProcessarArquivoEIngerir(stream, nome, profissao, userId);
            }
        }

        public Task IngerirConhecimento(Stream stream, string nome, string profissao, int userId)
        {
            return ProcessarArquivoEIngerir(stream, nome, profissao, userId);
        }

        public async Task<bool> ExcluirArquivo(int id)
        {
            return await ExcluirDocumentoAdmin(id);
        }

        public Task<bool> ExcluirArquivo(int id, int userId)
        {
            return ExcluirDocumento(id, userId);
        }
        
        // --- PROCESSAMENTO PRINCIPAL ---
        public async Task ProcessarArquivoEIngerir(Stream arquivoStream, string nomeArquivo, string profissao, int userId, bool isSystem = false)
        {
            if (_pinecone == null || _embeddingClient == null) throw new Exception("IA Services offline");

            try 
            {
                string extensao = Path.GetExtension(nomeArquivo).ToLower();
                string textoExtraido = "";

                // 1. Lógica de Leitura por Extensão
                if (extensao == ".pdf") 
                {
                    textoExtraido = ExtrairTextoPDF(arquivoStream);
                }
                else if (extensao == ".jpg" || extensao == ".png" || extensao == ".jpeg") 
                {
                    textoExtraido = ExtrairTextoImagem(arquivoStream);
                }
                else if (extensao == ".docx") 
                {
                    textoExtraido = ExtrairTextoDocx(arquivoStream);
                }
                else if (extensao == ".xlsx") // <--- NOVO: EXCEL
                {
                    textoExtraido = ExtrairTextoExcel(arquivoStream);
                }
                // <--- NOVO: Suporte a arquivos de código e web
                else if (new[] { ".txt", ".md", ".json", ".csv", ".html", ".css", ".js", ".cs", ".sql", ".py", ".xml" }.Contains(extensao))
                {
                    using (var reader = new StreamReader(arquivoStream))
                    {
                        if(arquivoStream.CanSeek) arquivoStream.Position = 0;
                        textoExtraido = await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    _logger.LogWarning($"Formato não suportado: {extensao}");
                    return; 
                }

                if (string.IsNullOrWhiteSpace(textoExtraido)) {
                    _logger.LogWarning($"Nenhum texto extraído: {nomeArquivo}");
                    return;
                }

                // 2. Salvar Metadados no Banco SQL
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

                // 3. Vetorização (Pinecone)
                var chunks = QuebrarTexto(textoExtraido, 1000);
                var vectors = new List<Vector>();
                var index = _pinecone.Index(_indexName);

                string pineconeUserId = isSystem ? "system" : userId.ToString();

                int chunkIndex = 0;
                foreach (var chunk in chunks)
                {
                    var embedding = await _embeddingClient.GenerateEmbeddingAsync(chunk);
                    float[] vetor = embedding.Value.Vector.ToArray();

                    var metadata = new Metadata
                    {
                        { "text", chunk }, 
                        { "tag", profissao }, 
                        { "userId", pineconeUserId },
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
                _logger.LogError(ex, "Erro no upload");
                throw;
            }
        }

        // --- MÉTODOS AUXILIARES ---

        private string ExtrairTextoDocx(Stream stream)
        {
            try
            {
                if (stream.CanSeek) stream.Position = 0;
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var entry = archive.GetEntry("word/document.xml");
                    if (entry != null)
                    {
                        using (var entryStream = entry.Open())
                        using (var reader = new StreamReader(entryStream))
                        {
                            string xmlContent = reader.ReadToEnd();
                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(xmlContent);
                            var sb = new StringBuilder();
                            foreach (XmlNode node in xmlDoc.GetElementsByTagName("w:t"))
                                sb.Append(node.InnerText + " ");
                            return sb.ToString();
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Erro DOCX"); }
            return "";
        }

        // NOVO: Extração simples de Excel (Lê as strings compartilhadas)
        private string ExtrairTextoExcel(Stream stream)
        {
            try
            {
                if (stream.CanSeek) stream.Position = 0;
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    // O Excel guarda a maioria dos textos aqui para economizar espaço
                    var entry = archive.GetEntry("xl/sharedStrings.xml");
                    if (entry != null)
                    {
                        using (var entryStream = entry.Open())
                        using (var reader = new StreamReader(entryStream))
                        {
                            string xmlContent = reader.ReadToEnd();
                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(xmlContent);
                            var sb = new StringBuilder();
                            // As strings ficam dentro de <t>
                            foreach (XmlNode node in xmlDoc.GetElementsByTagName("t"))
                                sb.Append(node.InnerText + " | ");
                            return sb.ToString();
                        }
                    }
                    else
                    {
                        // Se não tiver sharedStrings, tenta ler a Planilha 1 bruta (menos comum para texto)
                        var sheet1 = archive.GetEntry("xl/worksheets/sheet1.xml");
                        if (sheet1 != null) return "[Planilha Excel detectada, mas sem textos indexáveis simples]";
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Erro XLSX"); }
            return "";
        }

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

        public async Task<bool> ExcluirDocumento(int docId, int userId)
        {
            var doc = await _context.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.UserId == userId);
            if (doc == null) return false;
            return await ExecutarExclusao(doc);
        }

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
    }
}