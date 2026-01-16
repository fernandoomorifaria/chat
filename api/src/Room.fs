module Room

open User
open Database
open SqlFun

type Room =
    { Id: int
      Name: string
      Members: User list }

type RoomSummary =
    { Id: int
      Name: string
      MemberCount: int }

(* NOTE: Maybe I should have used JOIN but it would duplicate the room data *)
(* NOTE: Add option type *)
let getRoom: int -> Room AsyncDb =
    sql
        "
        select r.id as Id, r.name as Name from rooms r where r.id = @roomId;

        select u.id as Id, u.nickname as Nickname
        from users u
        inner join room_members rm on u.id = rm.user_id
        where rm.room_id = @roomId
    "
    >> AsyncDb.map (fun (r, m) -> { r with Members = m })

let listRooms: unit -> RoomSummary array AsyncDb =
    sql
        "
        select r.id as Id, r.name as Name, count(rm.user_id) as MemberCount
        from rooms r
        left join room_members rm on r.id = rm.room_id
        group by r.id, r.name
        "

let listIdsByUser: int -> int array AsyncDb =
    sql "select room_id from room_members where user_id = @userId"

let hasMember: (int * int) -> bool AsyncDb =
    sql "select count(1) from room_members where room_id = @roomId and user_id = @userId"
    >> AsyncDb.map (fun c -> c > 0)

let joinRoom: (int * int) -> unit AsyncDb =
    sql
        "insert into room_members (room_id, user_id) values (@roomId, @userId)
on conflict (room_id, user_id) do nothing"

let leaveRoom: (int * int) -> unit AsyncDb =
    sql "delete from room_members where room_id = @roomId and user_id = @userId"
