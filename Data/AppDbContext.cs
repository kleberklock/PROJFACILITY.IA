using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Models;

namespace PROJFACILITY.IA.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Agent> Agents { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<KnowledgeDocument> KnowledgeDocuments { get; set; }
        public DbSet<Prompt> Prompts { get; set; }
        
        // NOVO: Tabela para os prompts do sistema
        public DbSet<SystemPrompt> SystemPrompts { get; set; }
    }
}