module Chat.App

open System
open System.IO
open System.Text
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open Microsoft.EntityFrameworkCore
open System.Security.Claims
open Giraffe
open User
open Database
open BCrypt.Net
open Microsoft.Extensions.Configuration
open System.IdentityModel.Tokens.Jwt
open StackExchange.Redis
open Types
open ChatHub

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "Chat" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "Chat" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------
type JwtOptions = {
    Issuer: string
    Audience: string
    Secret: byte array
}

type Environment = {
    JwtOptions: JwtOptions
}

type LoginRequest = {
    [<JsonPropertyName("nickname")>]
    Nickname: string

    [<JsonPropertyName("password")>]
    Password: string
}

type RegisterRequest = {
    [<JsonPropertyName("nickname")>]
    Nickname: string

    [<JsonPropertyName("emailAddress")>]
    EmailAddress: string

    [<JsonPropertyName("password")>]
    Password: string
}

let authorize =
    requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

let generateToken (jwtOptions: JwtOptions) (user: User) =
    let descriptor = new SecurityTokenDescriptor()

    let handler = new JwtSecurityTokenHandler();

    (* Maybe add sub *)
    let claims = [|
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());
        new Claim(ClaimTypes.Name, user.Nickname);
        new Claim(ClaimTypes.Email, user.EmailAddress)
    |]

    descriptor.Subject <- new ClaimsIdentity(claims)
    descriptor.Expires <- DateTime.UtcNow.AddDays 30
    descriptor.Issuer <- jwtOptions.Issuer
    descriptor.Audience <- jwtOptions.Audience
    descriptor.SigningCredentials <- new SigningCredentials(new SymmetricSecurityKey(jwtOptions.Secret), SecurityAlgorithms.HmacSha256Signature)

    let token = handler.CreateToken descriptor

    handler.WriteToken token

let registerHandler: HttpHandler =
    fun next ctx ->
        task {
            let! request = ctx.BindJsonAsync<RegisterRequest>()
            let context = ctx.GetService<Context>()

            let hash = BCrypt.HashPassword request.Password

            let user: CreateUser = { Nickname = request.Nickname; EmailAddress = request.EmailAddress; Password = hash }

            let! user = User.create context user

            return! Successful.created (text (user.Id.ToString())) next ctx
        }

type LoginResponse = {
    [<JsonPropertyName("nickname")>]
    Nickname: string
}

let loginHandler (environment: Environment): HttpHandler =
    fun next ctx ->
        task {
            let! request = ctx.BindJsonAsync<LoginRequest>()
            let context = ctx.GetService<Context>()

            let! user = User.get context request.Nickname

            match user with
            | Some user ->
                if BCrypt.Verify(request.Password, user.Password) then
                    let token = generateToken environment.JwtOptions user

                    let bytes = Encoding.UTF8.GetBytes token

                    let cookieOptions = CookieOptions(
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddDays 30
                    )

                    ctx.Response.Cookies.Append("Token", Convert.ToBase64String bytes, cookieOptions)

                    return! Successful.ok (json { Nickname = user.Nickname }) next ctx
                else
                    return! RequestErrors.unauthorized JwtBearerDefaults.AuthenticationScheme "Chat" (text "Invalid user or password") next ctx
            | None -> return! RequestErrors.notFound (text "User not found") next ctx
        }

let messagedHandler (roomId: int): HttpHandler =
    authorize >=>
    fun next ctx ->
        task {
            let cache = ctx.GetService<IDatabase>()

            let historyKey = sprintf "room:%i:history" roomId

            let! history = cache.SortedSetRangeByRankWithScoresAsync historyKey

            let messages =
                history
                |> Array.map (fun message -> Json.deserialize<Message> (string message))

            return! Successful.ok (json messages) next ctx
        }

type CreateRoomRequest = {
    [<JsonPropertyName("name")>]
    Name: string
}

let createRoomHandler: HttpHandler =
    fun next ctx ->
        task {
            let! request = ctx.BindJsonAsync<CreateRoomRequest>()
            let context = ctx.GetService<Context>()

            let! room = Room.create context { Name = request.Name }

            return! Successful.created (text (room.Id.ToString())) next ctx
        }

type RoomResponse = {
    [<JsonPropertyName("id")>]
    Id: int;

    [<JsonPropertyName("name")>]
    Name: string;
}

let roomHandler: HttpHandler =
    fun next ctx ->
        task {
            let context = ctx.GetService<Context>()

            let! rooms = Room.getAll context

            let response = rooms |> Seq.map (fun room -> { Id = room.Id; Name = room.Name })

            return! Successful.ok (json response) next ctx
        }

let webApp (environment: Environment)=
    choose [
        GET >=>
            choose [
                routef "/room/%i/messages" messagedHandler
                route "/room" >=> authorize >=> roomHandler
            ]
        POST >=>
            choose [
                route "/login" >=> loginHandler environment
                route "/register" >=> registerHandler
                route "/room" >=> authorize >=> createRoomHandler
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:5173")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
        |> ignore

let configureApp (environment: Environment) (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseAuthentication()
        .UseStaticFiles()
        .UseRouting()
        .UseAuthorization()
        .UseEndpoints(fun endpoints -> endpoints.MapHub<ChatHub>("/ws").RequireAuthorization() |> ignore)
        .UseGiraffe(webApp environment)

let getTokenFromCookie (ctx: MessageReceivedContext): Task =
    task {
        let (found, value) = ctx.Request.Cookies.TryGetValue "Token"

        if found then
            let bytes = Convert.FromBase64String value

            let token = Encoding.UTF8.GetString bytes

            ctx.Token <- token
        else
            ctx.Token <- ""
    }

let configureServices (jwtOptions: JwtOptions) (services : IServiceCollection) =
    let events = JwtBearerEvents()

    events.OnMessageReceived <- getTokenFromCookie

    services.AddCors() |> ignore
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun options ->
            options.Events <- events
            options.TokenValidationParameters <- new TokenValidationParameters(
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(jwtOptions.Secret),
                ClockSkew = TimeSpan.Zero
            )
        ) |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    let configuration = builder.Configuration

    let database = configuration.GetConnectionString "Default"
    let redis = configuration.GetConnectionString "Redis"

    let issuer = configuration.GetValue<string> "Jwt:Issuer"
    let audience = configuration.GetValue<string> "Jwt:Audience"
    let secret = Encoding.UTF8.GetBytes(configuration.GetValue<string> "Jwt:Secret")

    builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>() |> ignore
    builder.Services.AddDbContextPool<Context>(fun options -> options.UseNpgsql(database) |> ignore) |> ignore
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redis, fun options -> options.Configuration.ChannelPrefix <- RedisChannel.Pattern "Chat_")
        .AddJsonProtocol(fun options -> options.PayloadSerializerOptions.Converters.Add(Json.MessageTypeConverter())) |> ignore

    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redis)) |> ignore

    builder.Services.AddTransient<IDatabase>(fun serviceProvider ->
        let redis = serviceProvider.GetRequiredService<IConnectionMultiplexer>()
        redis.GetDatabase()
    ) |> ignore

    let jwtOptions = {
        Issuer = issuer;
        Audience = audience;
        Secret = secret
    }

    configureServices jwtOptions builder.Services |> ignore
    
    configureLogging builder.Logging |> ignore

    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot = Path.Combine(contentRoot, "WebRoot")
    
    builder.Environment.ContentRootPath <- contentRoot
    builder.Environment.WebRootPath <- webRoot
    
    let app = builder.Build()

    let environment = {
        JwtOptions = jwtOptions
    }
    
    configureApp environment app |> ignore
    
    app.Run()

    0