module Types

open System
open System.Collections.Generic

[<CLIMutable>]
type User = {
    Id: int
    Nickname: string
    EmailAddress: string
    Password: string
    mutable Rooms: ICollection<Room>
}

and [<CLIMutable>]
Room = {
    Id: int
    Name: string
    mutable Users: ICollection<User>
}

type MessageType = Text | System

type Message = {
    Type: MessageType
    Content: string
    RoomId: int
    ConnectionId: string
    Nickname: string
    Timestamp: DateTime
}