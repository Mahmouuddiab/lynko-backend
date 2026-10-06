using System.Text;
using ChatApi.Data;
using ChatApi.Hubs;
using ChatApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Upload size limits (video max = 100 MB)
// ========================================

const long MaxUploadBytes = 100_000_000;

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = MaxUploadBytes;
});

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxUploadBytes;
});


// ========================================
// Controllers & SignalR
// ========================================

builder.Services.AddControllers();
builder.Services.AddSignalR();

// Optional: send/receive message "type" as a string ("Image") instead of a number (1).
// If you enable this, Flutter must use the same format. Keep it off to stay with numbers.
// builder.Services.AddControllers().AddJsonOptions(o =>
//     o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
// builder.Services.AddSignalR().AddJsonProtocol(o =>
//     o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));


// ========================================
// OpenAPI / Swagger
// ========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Chat API",
        Version = "v1"
    });

    // JWT Bearer Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    // Swagger Authorize Button
    c.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});


// ========================================
// Database Connection - MySQL
// ========================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    )
);


// ========================================
// Application Services
// ========================================

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IChatService, ChatService>();


// ========================================
// JWT Configuration
// ========================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured"
    );


// ========================================
// JWT Authentication
// ========================================

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ClockSkew = TimeSpan.Zero
            };

        // Configure SignalR to read access token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });


// ========================================
// Authorization
// ========================================

builder.Services.AddAuthorization();


// ========================================
// Reverse proxy support (Nginx / Cloudflare / hosting panel)
// Makes Request.Scheme report "https" so upload URLs are correct.
// ========================================

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust the proxy in front of you. Fine when the app is only reachable through it.
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});


// ========================================
// Build Application
// ========================================

var app = builder.Build();

app.UseForwardedHeaders();


// ========================================
// Swagger Middleware
// ========================================

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Chat API v1"
    );

    c.RoutePrefix = "swagger";
});


// ========================================
// HTTPS (Disabled for local HTTP testing)
// ========================================

// app.UseHttpsRedirection();


// ========================================
// Static files (serves /uploads/images, /uploads/videos, /uploads/voice)
// ========================================

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));
app.UseStaticFiles();


// ========================================
// Authentication & Authorization
// ========================================

app.UseAuthentication();
app.UseAuthorization();


// ========================================
// Endpoints & SignalR Hub
// ========================================

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapGet("/", () => "API is running!");


// ========================================
// Run Application
// ========================================

app.Run();