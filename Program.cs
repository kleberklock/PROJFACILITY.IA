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

// 2. Liberar CORS (MODO DESENVOLVIMENTO - LIBERA TUDO)
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

// 5. Pipeline de Execução
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

// O CORS deve vir DEPOIS do Routing e ANTES da Auth
app.UseRouting();
app.UseCors("AllowAll"); 

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --- Inicialização do Banco ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PROJFACILITY.IA.Data.AppDbContext>();
        
        // Aplica Migrations pendentes (Cria tabelas se não existirem)
        context.Database.Migrate(); 
        
        // Roda o Seeder
        PROJFACILITY.IA.Data.DbSeeder.Seed(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro ao popular o banco de dados.");
    }
}

app.Run();