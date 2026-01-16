module Message

open System
open System.Text.Json
open StackExchange.Redis

// TODO: Use ADT
// TODO: Create MessageEntity and include RoomId and ConnectionId
type Message =
    { Type: string
      Nickname: string
      Content: string
      Timestamp: DateTime }

let addMessage (cache: IDatabase) (roomId: int) (message: Message) =
    task {
        // TODO: Create a function for this
        let key = sprintf "room:%i:history" roomId

        let score = float DateTime.UtcNow.Ticks

        let json = JsonSerializer.Serialize message

        let! _ = cache.SortedSetAddAsync(key, json, score)

        ()
    }

let getMessages (cache: IDatabase) (roomId: int) =
    task {
        let key = sprintf "room:%i:history" roomId

        let! result = cache.SortedSetRangeByRankAsync key

        return
            result
            |> Array.map (fun message -> JsonSerializer.Deserialize<Message>(string message))
    }
