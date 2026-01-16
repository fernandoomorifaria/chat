module ChatHub

open System
open System.Threading.Tasks
open System.Security.Claims
open Microsoft.AspNetCore.SignalR
open Database
open Room
open Message
open StackExchange.Redis

type IChatClient =
    abstract member ReceiveMessage: Message -> Task
    abstract member ReceiveMessageHistory: Message array -> Task
    abstract member UserJoined: string -> Task
    abstract member UserLeft: string -> Task
    abstract member UserNotInRoom: string -> Task
    abstract member UserAlreadyInRoom: string -> Task
    abstract member RoomNotFound: string -> Task

let getUserId (principal: ClaimsPrincipal) =
    principal.FindFirst("local_id").Value |> int

// TODO: Do proper logging
type ChatHub(cache: IDatabase) =
    inherit Hub<IChatClient>()

    override this.OnConnectedAsync() =
        task {
            let userId = getUserId this.Context.User

            let! ids = listIdsByUser userId |> run

            for roomId in ids do
                do! this.Groups.AddToGroupAsync(this.Context.ConnectionId, string roomId)
        }

    member this.SendMessage (roomId: int) (content: string) =
        task {
            let nickname = this.Context.User.Identity.Name

            let message =
                { Type = "text"
                  Nickname = nickname
                  Content = content.TrimEnd()
                  Timestamp = DateTime.UtcNow }

            do! addMessage cache roomId message

            printfn "Message %s received from %s" message.Content nickname

            do! this.Clients.Group(string roomId).ReceiveMessage message
        }

    member this.JoinRoom(roomId: int) =
        task {
            let userId = getUserId this.Context.User

            let! isMember = hasMember (roomId, userId) |> run

            if not isMember then
                printfn "Adding user to group"

                do! this.Groups.AddToGroupAsync(this.Context.ConnectionId, string roomId)

                do! joinRoom (roomId, userId) |> run

                // TODO: I think I should return a Message in these cases
                do! this.Clients.Group(string roomId).UserJoined "User joined"

                let! messages = getMessages cache roomId

                printfn "Got %i messages from history" messages.Length

                do! this.Clients.Caller.ReceiveMessageHistory messages
        }

    member this.LeaveRoom(roomId: int) =
        task {
            let userId = getUserId this.Context.User

            let! isMember = hasMember (roomId, userId) |> run

            if isMember then
                printfn "Removing from group"

                do! this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, string roomId)

                do! leaveRoom (roomId, userId) |> run
        }
