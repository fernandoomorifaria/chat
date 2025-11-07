module User

open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Database
open Types

type CreateUser = {
    Nickname: string
    EmailAddress: string
    Password: string
}

let get (context: Context) (nickname: string) =
    task {
        let! user = context.Users.Include(fun u -> u.Rooms).SingleOrDefaultAsync(fun u -> u.Nickname = nickname)

        match box user with
        | null -> return None
        | _ -> return Some user
    }

let create (context: Context) (user: CreateUser) =
    task {
        let user: User = {
            Id = 0;
            Nickname = user.Nickname;
            EmailAddress = user.EmailAddress
            Password = user.Password;
            Rooms = List<Room>()
        }

        let! _ = context.Users.AddAsync user
        let! _ = context.SaveChangesAsync()

        return user
    }
