#I __SOURCE_DIRECTORY__
#r "../../packages/FirebirdSql.Data.FirebirdClient.5.11.0/lib/net452/FirebirdSql.Data.FirebirdClient.dll"
#r "../../packages/SQLProvider.1.1.19/lib/net451/FSharp.Data.SqlProvider.dll"

#load "Types.fs"
#load "Util.fs"
#load "Input.fs"

open FirebirdSql.Data.FirebirdClient
open System
open System.IO
open FSharp.Data.Sql
open FbDiff.Util
open FbDiff.Types
open FbDiff.Input

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

let getAllTables () = 
    let srcCtx = HR.GetDataContext(srcConnection)
    let trgCtx = HR.GetDataContext(trgConnection)
    (getTables srcCtx, getTables trgCtx)
let (srcTables, trgTables) = getAllTables()

let getTable tableName l = l|> Seq.find (fun f->f.Name.unbox = tableName)
let equal (expected:'a) (actual: 'a) = if not (expected = actual) then failwithf "expected %A, got %A" expected actual
let has s (list: 'a seq) (p: 'a -> string) = if not (list |> Seq.map p|> Seq.contains s) then failwithf "expected to find %s, in %A" s list
let hasField table fieldName = if not (table.Fields |> Seq.map (fun f -> f.Name.unbox) |>Seq.contains fieldName) then failwithf "expected to find field %s, in table %s" fieldName table.Name.unbox
let hasFk fkName table = if not (table.Fks |> Seq.exists (fun fk->fk.Name.unbox = fkName)) then failwithf "expected to find fk %s for table %s" fkName table.Name.unbox

equal 2 srcTables.Length
has "MISSING_TABLE" srcTables (fun x->x.Name.unbox)
let missingTable = getTable "MISSING_TABLE" srcTables
hasField missingTable "ID"
hasField missingTable "NAME"
hasField missingTable "NAME1"
has "DIFF_TABLE" srcTables (fun x->x.Name.unbox)
let srcDiffTable = getTable "DIFF_TABLE" srcTables
hasField srcDiffTable "ID"
hasField srcDiffTable "NAME"
hasField srcDiffTable "NAME1"
hasField srcDiffTable "DIFF_LEN"
hasField srcDiffTable "DIFF_NULLABILITY"
hasField srcDiffTable "WITH_CHECK_CONSTRAINT"
equal 2 trgTables.Length
has "DIFF_TABLE" trgTables (fun x->x.Name.unbox)
let trgDiffTable = getTable "DIFF_TABLE" trgTables
hasFk "FK_TO_DROP" trgDiffTable
has "TABLE_TO_DROP" trgTables (fun x->x.Name.unbox)