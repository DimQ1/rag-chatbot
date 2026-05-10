using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using rag_chatbot_api.Data;
using rag_chatbot_api.Options;
using rag_chatbot_api.Services;

const string AngularCorsPolicy = "AngularApp";

var builder = WebApplication.CreateBuilder(args);

ConfigureOptions(builder.Services, builder.Configuration);
ConfigureDataAccess(builder.Services, builder.Configuration);
ConfigureApplicationServices(builder.Services);
ConfigureCors(builder.Services);
ConfigureAuthentication(builder.Services, ResolveJwtOptions(builder.Configuration));

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

await AppDbInitializer.InitializeAsync(app.Services);
ConfigureHttpPipeline(app);

app.Run();

static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
    services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
    services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
    services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
}

static JwtOptions ResolveJwtOptions(IConfiguration configuration)
{
    return configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Jwt configuration is missing.");
}

static void ConfigureDataAccess(IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));
}

static void ConfigureApplicationServices(IServiceCollection services)
{
    services.AddScoped<ITokenService, TokenService>();
    services.AddScoped<IRagIndexService, RagIndexService>();
    services.AddScoped<IRagService, RagService>();
    services.AddScoped<IChatSessionService, ChatSessionService>();
    services.AddSingleton<IAgentSessionStore, AgentSessionStore>();
}

static void ConfigureCors(IServiceCollection services)
{
    services.AddCors(options =>
    {
        options.AddPolicy(AngularCorsPolicy, policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

static void ConfigureAuthentication(IServiceCollection services, JwtOptions jwtOptions)
{
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
            };
        });
}

static void ConfigureHttpPipeline(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseCors(AngularCorsPolicy);
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
}

public partial class Program;
