using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Middlewares;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Banco de Dados
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Liberar CORS (Permite Azure e seu ambiente local de desenvolvimento)
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors",
        builder => builder
            .WithOrigins(
                "https://facility-ia-frg6cqbcggasdhea.centralus-01.azurewebsites.net", // Produção
                "http://localhost:5217",   // Frontend local (Live Server padrão)
                "http://127.0.0.1:5500",   // Frontend local IP
                "http://localhost:5217"    // Backend local
            )
            .AllowAnyMethod()
            .AllowAnyHeader());
});
// 3. Configurar Serviços
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddControllers();

// 4. Configurar JWT
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    // O sistema DEVE parar se não houver chave segura configurada na Azure
    throw new InvalidOperationException("FATAL: A chave JWT não foi configurada nas variáveis de ambiente ou é muito curta!");
}

var key = Encoding.ASCII.GetBytes(jwtKey);
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = true; // Obrigatório em produção
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// ==========================================
// LINHA CRÍTICA QUE ESTAVA A FALTAR
var app = builder.Build();
// ==========================================

// 5. Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Força o uso de HTTPS em produção
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("ProductionCors");

app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// LINHA CRÍTICA QUE TINHA DESAPARECIDO
app.MapControllers();
// ==========================================

// --- INICIALIZAÇÃO BLINDADA DO BANCO DE DADOS ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<PROJFACILITY.IA.Data.AppDbContext>();

    // 1. Tenta atualizar a estrutura do banco (Criar tabelas)
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        // Se falhar (ex: tabela já existe em conflito), apenas avisa no log e CONTINUA
        logger.LogWarning(ex, "Aviso: O Migrate encontrou um conflito ou já estava atualizado.");
    }

    // 2. Tenta preencher os dados (Prompts) INDEPENDENTE do passo anterior
    try
    {
        PROJFACILITY.IA.Data.DbSeeder.Seed(context);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro crítico ao popular o banco de dados (Seeder).");
    }
}
// ------------------------------------------------

app.Run();