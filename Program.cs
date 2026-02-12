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

// 2. Liberar CORS (Permite acesso do Frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// 3. Configurar Serviços
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddControllers();

// 4. Configurar JWT
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? "chave_super_secreta_padrao_para_desenvolvimento_123");
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

var app = builder.Build();

// 5. Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowAll"); 

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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