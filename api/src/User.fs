module User

open Database
open SqlFun

type User = { Id: int; Nickname: string }

type CreateUser =
    { Nickname: string; ExternalId: string }

let getUser: string -> User option AsyncDb =
    sql "select u.id as Id, u.nickname as Nickname from users u where u.external_id = @external_id"

let createUser: CreateUser -> int AsyncDb =
    sql
        "insert into users (nickname, external_id)
values (@Nickname, @ExternalId) returning id"
