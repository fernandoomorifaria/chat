module ChatHub

open System
open System.Linq
open System.Threading.Tasks
open System.Security.Claims
open Microsoft.AspNetCore.SignalR
open StackExchange.Redis
open Database
open Types
open Json

type IChatClient =
    abstract member ReceiveMessage: Message -> Task
    abstract member UserJoined: string -> Task
    abstract member UserLeft: string -> Task
    abstract member UserNotInRoom: string -> Task
    abstract member UserAlreadyInRoom: string -> Task
    abstract member RoomNotFound: string -> Task

type ChatHub (context: Context, cache: IDatabase) =
    inherit Hub<IChatClient>()

    override this.OnConnectedAsync (): Task =
        task {
            let nickname = this.Context.User.FindFirst(ClaimTypes.Name).Value

            let! user = User.get context nickname

            match user with
            | Some user ->
                for room in user.Rooms do
                    do! this.Groups.AddToGroupAsync(this.Context.ConnectionId, room.Id.ToString())
            | None -> ()
        }

    member this.SendMessage (roomId: int) (message: string) =
        task {
            let nickname = this.Context.User.FindFirst(ClaimTypes.Name).Value

            let! room = Room.get context roomId

            (* Move to a global variable *)
            let historyKey = sprintf "room:%i:history" roomId

            if room.Users.Any(fun u -> u.Nickname = nickname) then
                let message = {
                    Type = Text
                    Content = message;
                    RoomId = roomId;
                    ConnectionId = this.Context.ConnectionId;
                    Nickname = nickname;
                    Timestamp = DateTime.UtcNow
                }

                let json = serialize message

                let score = float DateTime.UtcNow.Ticks

                do! this.Clients.Group(roomId.ToString()).ReceiveMessage message

                let! _ = cache.SortedSetAddAsync(historyKey, json, score)
                
                ()
            else
                do! this.Clients.Caller.UserNotInRoom (sprintf "%s is not in room %s" nickname room.Name)
        }

    member this.JoinRoom (roomId: int) =
        task {
            let nickname = this.Context.User.FindFirst(ClaimTypes.Name).Value

            let! room = Room.get context roomId

            if room.Users.Any(fun u -> u.Nickname = nickname) then
                do! this.Clients.Caller.UserAlreadyInRoom (sprintf "%s is already in room %s" nickname room.Name)
            else
                let! user = User.get context nickname

                match user with
                | Some user ->
                    (* This will raise an exception and will not send a message to client saying that there's no room *)
                    do! Room.joinRoom context user roomId
                    
                    do! this.Groups.AddToGroupAsync(this.Context.ConnectionId, roomId.ToString())
                    
                    do! this.Clients.Group(roomId.ToString()).UserJoined (sprintf "%s has joined" nickname)

                    (*
                        Create an endpoint for this
                    let historyKey = sprintf "room:%i:history" roomId

                    let! history = cache.SortedSetRangeByRankWithScoresAsync historyKey

                    let messages =
                        history
                        |> Array.map (fun message -> deserialize<Message> (string message))

                    do! this.Clients.Caller.ReceiveMessageHistory messages
                    *)
                | None ->
                    do! this.Clients.Caller.RoomNotFound "Room not found"
        }

    member this.LeaveRoom (roomId: int) =
        task {
            let nickname = this.Context.User.FindFirst(ClaimTypes.Name).Value
            
            let! user = User.get context nickname

            match user with
            | Some user ->
                do! Room.leaveRoom context user roomId

                do! this.Clients.Group(roomId.ToString()).UserLeft (sprintf "%s has left the room" nickname)

                do! this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, roomId.ToString())
            | None -> ()
        }