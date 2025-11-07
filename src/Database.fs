module Database

open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata.Builders
open Types

type Context(options: DbContextOptions<Context>) =
    inherit DbContext(options)

    [<DefaultValue>]
    val mutable users: DbSet<User>

    [<DefaultValue>]
    val mutable rooms: DbSet<Room>

    member public this.Users with get() = this.users and set users = this.users <- users
    member public this.Rooms with get() = this.rooms and set rooms = this.rooms <- rooms

    override this.OnModelCreating (modelBuilder: ModelBuilder): unit = 
        modelBuilder.Entity<User>(fun entity ->
            entity.ToTable "users" |> ignore
            entity.HasKey(fun u -> u.Id :> obj) |> ignore
            entity.Property(fun u -> u.Id).HasColumnName "id" |> ignore
            entity.Property(fun u -> u.Nickname).HasColumnName "nickname" |> ignore
            entity.Property(fun u -> u.EmailAddress).HasColumnName "email" |> ignore
            entity.Property(fun u -> u.Password).HasColumnName "password" |> ignore
        ) |> ignore

        modelBuilder.Entity<Room>(fun (entity: EntityTypeBuilder<Room>) ->
            entity.ToTable "rooms" |> ignore
            entity.HasKey(fun r -> r.Id :> obj) |> ignore
            entity.Property(fun r -> r.Id).HasColumnName "id" |> ignore
            entity.Property(fun r -> r.Name).HasColumnName "name" |> ignore
            
            entity.HasMany<User>(fun r -> r.Users :> IEnumerable<User>)
                  .WithMany(fun u -> u.Rooms :> IEnumerable<Room>)
                  .UsingEntity(fun (j: EntityTypeBuilder) ->
                      j.ToTable "room_members" |> ignore

                      j.Property<int>("RoomId").HasColumnName "room_id" |> ignore
                      j.Property<int>("UserId").HasColumnName "user_id" |> ignore
                      j.HasKey("RoomId", "UserId") |> ignore

                      j.HasOne(typeof<Room>).WithMany().HasForeignKey "RoomId" |> ignore
                      j.HasOne(typeof<User>).WithMany().HasForeignKey "UserId" |> ignore
                  ) |> ignore
        ) |> ignore