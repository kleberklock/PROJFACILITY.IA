# PROJFACILITY.IA - Plataforma de IA com RAG

O **PROJFACILITY.IA** é um ecossistema de Inteligência Artificial desenvolvido em **.NET 8** focado em personalização de agentes e gestão de conhecimento através de **RAG (Retrieval-Augmented Generation)**. A plataforma permite carregar documentos, vetorizá-los e utilizá-los como base de dados primária para respostas de IA precisas.

## 🚀 Funcionalidades

- **Agentes Customizáveis**: Criação de assistentes com personalidades e instruções de sistema específicas.
- **RAG First**: Arquitetura desenhada para consultar primeiro a base de conhecimento local antes de recorrer ao conhecimento geral da IA.
- **Ingestão Inteligente de Dados**:
  - Suporte para `PDF`, `DOCX`, `XLSX` e formatos de texto.
  - OCR integrado para leitura de texto em imagens (`JPG`, `PNG`).
  - Divisão automática de texto em chunks para otimização de busca.
- **Gestão de Sessões**: Histórico de chat persistente e suporte para múltiplas conversas em paralelo.
- **Sistema de Planos**: Limitação de tokens mensal baseada no perfil do utilizador (Free, Pro, Enterprise).
- **Pagamentos**: Integração com Stripe para subscrições.

## 🛠️ Tecnologias Utilizadas

- **Backend**: .NET 8 Web API
- **IA**: OpenAI (GPT-4o-mini e Embeddings `text-embedding-3-small`)
- **Base de Dados Vetorial**: Pinecone
- **Base de Dados Relacional**: SQL Server (Entity Framework Core)
- **OCR**: Tesseract
- **Segurança**: Autenticação JWT (JSON Web Tokens)

## 📂 Estrutura do Repositório

- `Controllers/`: Endpoints da API para Chat, Conhecimento e Administração.
- `Services/`: Lógica centralizada de integração com IA (ChatService) e processamento de ficheiros (KnowledgeService).
- `Models/`: Entidades de dados e DTOs.
- `Migrations/`: Histórico de evolução da base de dados SQL.
- `wwwroot/`: Interface web estática integrada.
