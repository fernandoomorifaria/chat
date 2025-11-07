module Room

open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Types
open Database

type CreateRoom = {
    Name: string    
}

let get (context: Context) (roomId: int) =
    task {
        let! room = context.Rooms.Include(fun r -> r.Users).SingleOrDefaultAsync(fun r -> r.Id = roomId)
    
        return room
    }

let getAll (context: Context) =
    task {
        let! rooms = context.Rooms.ToArrayAsync()

        return rooms
    }

let create (context: Context) (room: CreateRoom) =
    task {
        let room = {
            Id = 0;
            Name = room.Name;
            Users = List<User>()
        }

        let! _ = context.Rooms.AddAsync room
        let! _ = context.SaveChangesAsync()

        return room
    }

exception RoomNotFound of string

let joinRoom (context: Context) (user: User) (roomId: int) =
    task {
        let! room = context.Rooms.Include(fun r -> r.Users).SingleOrDefaultAsync(fun r -> r.Id = roomId)

        (* Use result type *)
        match box room with
        | null -> raise (RoomNotFound("Room not found"))
        | _ ->
            room.Users.Add user

            let! _ = context.SaveChangesAsync()

            ()
    }

let leaveRoom (context: Context) (user: User) (roomId: int) =
    task {
        let! room = context.Rooms.Include(fun r-> r.Users).SingleOrDefaultAsync(fun r -> r.Id = roomId)

        (* Use result type *)
        match box room with
        | null -> raise (RoomNotFound("Room not found"))
        | _ ->
            room.Users.Remove user |> ignore

            let! _ = context.SaveChangesAsync()

            ()
    }