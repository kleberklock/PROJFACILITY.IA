using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Middlewares;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PROJFACILITY.IA.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.CaptureStartupErrors(true);
builder.WebHost.UseSetting("detailedErrors", "true");

// 1. Configurar Banco de Dados
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("FALTA A CONNECTION STRING NO AZURE!");
    }
}
else if (string.IsNullOrEmpty(connectionString))
{
    // Fallback apenas para desenvolvimento local
    connectionString = "Server=(localdb)\\mssqllocaldb;Database=ProjFacilityIADb_Local_DB;Trusted_Connection=True;MultipleActiveResultSets=true";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptions => 
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// 2. Liberar CORS (Configurável via AppSettings/Azure)
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors",
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader());
});
// 3. Configurar Serviços
builder.Services.Configure<EmailSettingsOptions>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddControllers();

// 4. Configurar JWT
var jwtKey = builder.Configuration["Jwt:Key"];

// Em Produção, o sistema DEVE parar se não houver chave segura (mínimo 32 caracteres)
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
    {
        throw new InvalidOperationException("ERRO FATAL: A chave 'Jwt:Key' de PRODUÇÃO não foi configurada no Azure (mínimo 32 caracteres)!");
    }
}
else if (string.IsNullOrEmpty(jwtKey))
{
    // Fallback apenas para desenvolvimento local
    jwtKey = "DEV_SECRET_KEY_FOR_LOCAL_TESTING_32_CHARS";
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
//app.UseMiddleware<ExceptionHandlingMiddleware>();
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