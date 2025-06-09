using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// 🔑 Secret key for signing JWT
var Key = "my-very-strong-secret-key-1234567890!!";


builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// ✅ Setup EF Core with SQLite
builder.Services.AddDbContext<TodoDbcontext>(options =>
    options.UseSqlite("Data Source=TodoList.db"));

// ✅ Add Authentication with JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// ✅ Middlewares
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));

app.UseAuthentication();
app.UseAuthorization();

// Middleware to intercept and log incoming POST data
app.Use(async (context, next) =>
{
    Console.WriteLine("📡 Started Sniffing Incoming Data...");
    var ip = context.Connection.RemoteIpAddress?.ToString();
    var UA = context.Request.Headers["User-Agent"].ToString();
    var Path = context.Request.Path;

    // We log response code after response, so capture here for logging after next()
    int responseCode = 0;
    string body = string.Empty;

    if (context.Request.Method == "POST")
    {
        context.Request.EnableBuffering();
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reset stream position for downstream
        }

        Console.WriteLine("🔍 VICTIM DATA INTERCEPTED:");
        Console.WriteLine($"IP         : {ip}");
        Console.WriteLine($"Path       : {Path}");
        Console.WriteLine($"User-Agent : {UA}");
        Console.WriteLine($"Body       : {body}");
    }

    await next();

    responseCode = context.Response.StatusCode;

    Console.WriteLine($"Response code: {responseCode}");
    Console.WriteLine("Logging the collected data to a file...");
    await File.AppendAllTextAsync("logs.txt",
        $"{DateTime.Now:dd-MM-yyyy hh.mm.ss tt} - IP: {ip}, Path: {Path}, User-Agent: {UA}, Response Code: {responseCode} Body: {body} \n");
    Console.WriteLine("Successfully logged to file: logs.txt");
});

// ✅ In-memory user list
List<User> users = new();

users.Add(new User { Username = "dheeraj", Password = "123456" });

// 🔐 SIGNUP Endpoint
app.MapPost("/signup", (Usercreds creds) =>
{
    if (users.Any(u => u.Username == creds.Username))
        return Results.BadRequest("User already exists");

    users.Add(new User { Username = creds.Username, Password = creds.Password });
    return Results.Ok("New User created successfully");
});

// 🔐 LOGIN Endpoint (issues JWT token)
app.MapPost("/login", (Usercreds creds) =>
{
    var user = users.FirstOrDefault(u => u.Username == creds.Username && u.Password == creds.Password);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, user.Username)
    };

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenKey = Encoding.UTF8.GetBytes(Key);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    return Results.Ok(new { token = tokenString });
});

// 🔐 Protected Endpoint
app.MapGet("/me", (HttpContext httpContext) =>
{
    var username = httpContext.User.Identity?.Name;
    return Results.Ok($"Hello {username}, you're authenticated!");
}).RequireAuthorization();

// ✅ CRUD for Todos
app.MapGet("/todos", async (TodoDbcontext db) => await db.Todos.ToListAsync());

app.MapGet("/todos/{id}", async (int id, TodoDbcontext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    return todo is not null ? Results.Ok(todo) : Results.NotFound();
});

app.MapPost("/todos", async (Todo todo, TodoDbcontext db) =>
{
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return TypedResults.Created($"/todos/{todo.Id}", todo);
})
.AddEndpointFilter(async (context, next) =>
{
    // Enable buffering to read the request body multiple times
    context.HttpContext.Request.EnableBuffering();

    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();

    if (taskArgument.DueDate < DateTime.UtcNow)
        errors.Add(nameof(Todo.DueDate), new[] { "Cannot have due date in the past." });

    if (taskArgument.IsComplete)
        errors.Add(nameof(Todo.IsComplete), new[] { "Cannot add completed task." });

    return errors.Count > 0
        ? Results.ValidationProblem(errors)
        : await next(context);
});

app.MapPost("/todos/bulk", async (List<Todo> todos, TodoDbcontext db) =>
{
    db.Todos.AddRange(todos);
    await db.SaveChangesAsync();
    return Results.Created("/todos", todos);
});

app.MapDelete("/todos/{id}", async (int id, TodoDbcontext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null) return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();


// ✅ Models

public class Todo
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsComplete { get; set; }
}

public class TodoDbcontext : DbContext
{
    public TodoDbcontext(DbContextOptions<TodoDbcontext> options) : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}

record Usercreds(string Username, string Password);

class User
{
    public string Username { get; set; }
    public string Password { get; set; }
}
