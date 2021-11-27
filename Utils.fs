module Utils

open System
open System.Data
open DataTables

let registerUser (userId:string) = 
    userDT.Rows.Add(userid) |> ignore
    usersTable.AcceptChanges()