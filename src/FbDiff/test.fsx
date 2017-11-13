#I __SOURCE_DIRECTORY__
#r "../../packages/FirebirdSql.Data.FirebirdClient.5.11.0/lib/net452/FirebirdSql.Data.FirebirdClient.dll"
#r "../../packages/SQLProvider.1.1.19/lib/net451/FSharp.Data.SqlProvider.dll"

#load "Types.fs"
#load "Util.fs"
#load "Input.fs"
#load "Output.fs"

open FirebirdSql.Data.FirebirdClient
open System
open System.IO
open FSharp.Data.Sql
open FbDiff.Util
open FbDiff.Types
open FbDiff.Input
open FbDiff.Output

let measure f = 
    let timer = System.Diagnostics.Stopwatch()
    timer.Start()
    let returnValue = f()
    (returnValue, timer.ElapsedMilliseconds)

Environment.CurrentDirectory <- Path.GetFullPath(__SOURCE_DIRECTORY__)
let csb = new FbConnectionStringBuilder()
csb.Dialect <- 3
csb.Charset <- "UTF8"
csb.UserID <- "SYSDBA"
csb.Password <- "masterkey"
//csb.ServerType <- FbServerType.Embedded
csb.Role <- ""

csb.Database <- __SOURCE_DIRECTORY__ + "\\src.fdb"
let srcConnection = csb.ConnectionString
csb.Database <- __SOURCE_DIRECTORY__ + "\\trg.fdb"
let trgConnection = csb.ConnectionString

//FSharp.Data.Sql.Common.QueryEvents.SqlQueryEvent |> Event.add (fun e -> System.Console.WriteLine (sprintf "Executing SQL: %O" e))

let getAllTables () = 
    let srcCtx = HR.GetDataContext(srcConnection)
    let trgCtx = HR.GetDataContext(trgConnection)
    (getTables srcCtx, getTables trgCtx)
let ((srcTables, trgTables), duration) = measure getAllTables
printfn "src tables (%d): %s" (srcTables |> Seq.length) (srcTables |> Seq.map (fun x -> x.Name.unbox) |> mkstring ",")
printfn "trg tables (%d): %s" (trgTables |> Seq.length) (trgTables |> Seq.map (fun x -> x.Name.unbox) |> mkstring ",")
printfn "Loading time: %i ms (oh, did I tell you about doing way too many queries?)" duration

let diff src trg =
    let (missingTables, differingTables, errors) = createDiff src trg
    printfn "ERRORS:\n%s\n" (errors
                                |> Seq.map (fun (tableName, fields) -> sprintf "table %s:\n%s" tableName.unbox (fields |> Seq.map (fun (field, err) -> sprintf "\t%s: %s" field.SrcField.Name.unbox err)|>mkstring ",\n"))
                                |> mkstring "\n")
    createScript (missingTables, differingTables)

let (sql, compareDuration) = measure (fun () -> diff srcTables trgTables)

let div = "==========================="
printfn "SCRIPT:\n%s\n%s\n%s" div (mkstring "\n" sql) div
printfn "Diff time: %i ms" compareDuration
