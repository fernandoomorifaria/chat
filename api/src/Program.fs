open Falco
open Falco.Routing
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Microsoft.AspNetCore.Cors.Infrastructure
open System.Security.Claims
open ChatHub
open User
open Room
open Database
open StackExchange.Redis

let roomsHandler: HttpHandler =
    let listRooms: HttpHandler =
        fun ctx ->
            task {
                let! rooms = listRooms () |> run

                return! Response.ofJson rooms ctx
            }

    Request.ifAuthenticated JwtBearerDefaults.AuthenticationScheme listRooms

let provisionUser (context: TokenValidatedContext) : Task =
    task {
        let nickname = context.Principal.Identity.Name
        let sub = context.Principal.FindFirst(ClaimTypes.NameIdentifier).Value

        let! user = getUser sub |> run

        // TODO: Find a better way to do this
        let! id =
            match user with
            | Some u -> Task.FromResult u.Id
            | None ->
                createUser
                    { Nickname = nickname
                      ExternalId = sub }
                |> run
                |> Async.StartAsTask

        let claim = Claim("local_id", string id)
        let identity = context.Principal.Identity :?> ClaimsIdentity

        identity.AddClaim claim
    }

let getAccessTokenFromQuery (context: MessageReceivedContext) =
    let accessToken = context.Request.Query.["access_token"]

    context.Token <- accessToken

    Task.CompletedTask

// TODO: Get configuration from appsettings.json
let configureJwt (services: IServiceCollection) =
    services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun options ->
            options.Authority <- "http://localhost:8080/realms/chat"
            options.Audience <- "account"
            options.RequireHttpsMetadata <- false

            options.Events <-
                JwtBearerEvents(OnMessageReceived = getAccessTokenFromQuery, OnTokenValidated = provisionUser)

            options.TokenValidationParameters <-
                TokenValidationParameters(
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    NameClaimType = "preferred_username"
                ))
    |> ignore

    services.AddAuthorization() |> ignore

    services

let configureCors (builder: CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:3000").AllowAnyMethod().AllowAnyHeader().AllowCredentials()
    |> ignore

let bldr = WebApplication.CreateBuilder()

configureJwt bldr.Services |> ignore

bldr.Services.AddCors().AddSignalR() |> ignore

let redis = "localhost:6379"

bldr.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redis))
|> ignore

bldr.Services.AddTransient<IDatabase>(fun serviceProvider ->
    let redis = serviceProvider.GetRequiredService<IConnectionMultiplexer>()
    redis.GetDatabase())
|> ignore

let wapp = bldr.Build()

wapp.MapHub<ChatHub>("/hub").RequireAuthorization() |> ignore

wapp.UseRouting().UseCors(configureCors).UseAuthentication().UseAuthorization().UseFalco [ get "/rooms" roomsHandler ]
|> ignore

wapp.Run()
