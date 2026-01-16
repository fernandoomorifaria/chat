module Database

open Npgsql
open SqlFun
open SqlFun.Queries
open SqlFun.NpgSql

(* TODO: Get connection string from appsettings.json *)
let createConnection () =
    let dataSource =
        NpgsqlDataSource.Create "Host=localhost;Port=5432;Database=chat;Username=postgres;Password=secret"

    dataSource.CreateConnection()

let generatorConfig = createDefaultConfig createConnection

let sql commandText = sql generatorConfig commandText

let run f = AsyncDb.run createConnection f
