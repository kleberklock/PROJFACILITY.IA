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
        
        // NOVO: Tabela de Prompts
        public DbSet<Prompt> Prompts { get; set; } 
    }
}