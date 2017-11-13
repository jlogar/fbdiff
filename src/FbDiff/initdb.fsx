#r "System.IO.Compression.dll"
#r "System.IO.Compression.FileSystem.dll"
#I __SOURCE_DIRECTORY__
#r "../../packages/FirebirdSql.Data.FirebirdClient.5.11.0/lib/net452/FirebirdSql.Data.FirebirdClient.dll"
#r "../../packages/System.IO.Compression.ZipFile.4.3.0/lib/net46/System.IO.Compression.ZipFile.dll"

#load "Util.fs"

open FirebirdSql.Data.FirebirdClient
open System.IO
open System
open FbDiff
open System.Net
open System.IO.Compression

[<Literal>]
let srcFileName = "src.fdb"
[<Literal>]
let trgFileName = "trg.fdb"
[<Literal>]
let fbDir = "Firebird-2.5.7.27050-0_x64_embed"
[<Literal>]
let fbFile = fbDir + ".zip"

Environment.CurrentDirectory <- Path.GetFullPath(__SOURCE_DIRECTORY__)

let dlFb = fun () ->
    use wc = new WebClient()
    wc.DownloadFile("https://downloads.sourceforge.net/project/firebird/firebird-win64/2.5.7-Release/Firebird-2.5.7.27050-0_x64_embed.zip", fbFile)
    ZipFile.ExtractToDirectory(fbFile, fbDir)

if not (Directory.Exists(fbDir)) then
    printfn "%s don't exist... dl-ing FB embedded server" fbDir
    dlFb()
let srcDbFile = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, srcFileName))
let trgDbFile = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, trgFileName))

let csb = new FbConnectionStringBuilder()
csb.Dialect <- 3
csb.Charset <- "UTF8"
csb.Database <- srcDbFile
csb.UserID <- "SYSDBA"
csb.Password <- "masterkey"
csb.ServerType <- FbServerType.Embedded
csb.Role <- ""
let srcConnectionString = csb.ConnectionString
csb.Database <-  trgDbFile
let trgConnectionString = csb.ConnectionString

//prepare the embeded binaries as per https://www.firebirdsql.org/pdfmanual/html/ufb-cs-embedded.html
if not (File.Exists "fbembed.dll") then
    let srcDir = Path.GetFullPath(sprintf "%s/../../%s/" __SOURCE_DIRECTORY__ fbDir)
    let trgDir = Path.GetFullPath(__SOURCE_DIRECTORY__)
    printfn "%s" (Path.GetFullPath(srcDir))
    File.Copy(Path.Combine(srcDir, "fbembed.dll"), Path.Combine(trgDir, "fbembed.dll"))
    File.Copy(Path.Combine(srcDir, "firebird.msg"), Path.Combine(trgDir, "firebird.msg"))
    File.Copy(Path.Combine(srcDir, "icudt30.dll"), Path.Combine(trgDir, "icudt30.dll"))
    File.Copy(Path.Combine(srcDir, "icuin30.dll"), Path.Combine(trgDir, "icuin30.dll"))
    File.Copy(Path.Combine(srcDir, "icuuc30.dll"), Path.Combine(trgDir, "icuuc30.dll"))
    File.Copy(Path.Combine(srcDir, "Microsoft.VC80.CRT.manifest"), Path.Combine(trgDir, "Microsoft.VC80.CRT.manifest"))
    File.Copy(Path.Combine(srcDir, "msvcp80.dll"), Path.Combine(trgDir, "msvcp80.dll"))
    File.Copy(Path.Combine(srcDir, "msvcr80.dll"), Path.Combine(trgDir, "msvcr80.dll"))

//MAKE SURE THIS RUNS as 64bit (using the 64bit embedded server)
if File.Exists srcDbFile then
    FbConnection.ClearAllPools();
    FbConnection.DropDatabase(srcConnectionString);
FbConnection.CreateDatabase(srcConnectionString, true)
if File.Exists trgDbFile then
    FbConnection.ClearAllPools();
    FbConnection.DropDatabase(trgConnectionString);
FbConnection.CreateDatabase(trgConnectionString, true)

let srcMissingTableFields = 
    [
    "ID INTEGER NOT NULL PRIMARY KEY"
    "NAME VARCHAR(255)"
    "NAME1 VARCHAR(255) NOT NULL"
    ]|> Util.mkstring ","
let srcDiffTableFields = 
    [
    "ID INTEGER NOT NULL PRIMARY KEY"
    "NAME VARCHAR(255)"
    "NAME1 VARCHAR(255) NOT NULL"
    "DIFF_LEN varchar(10)"
    "DIFF_NULLABILITY integer"
    "WITH_UNQ_CONSTRAINT integer CONSTRAINT UNQ_CONSTRAINT UNIQUE"
    "WITH_CHECK_CONSTRAINT integer CONSTRAINT CHECK_CONSTRAINT CHECK (1>DIFF_NULLABILITY)"
    ]|> Util.mkstring ","
let trgDiffTableFields = 
    [
    "ID INTEGER NOT NULL PRIMARY KEY"
    "DIFF_LEN varchar(255)"
    "DIFF_NULLABILITY integer NOT NULL"
    "FK_ID integer"
    ]|> Util.mkstring ","
let trgTableToDropFields = 
    [
    "ID INTEGER NOT NULL PRIMARY KEY"
    ]|> Util.mkstring ","
let runSrcDdl =
    use connection = new FbConnection(srcConnectionString)
    connection.Open()
    use tx = connection.BeginTransaction()
    let executeCmd text =
        printfn "%s" text
        let cmd = new FbCommand(text, connection, tx)
        cmd.CommandType <- Data.CommandType.Text
        cmd.ExecuteNonQuery()
    executeCmd (sprintf "CREATE TABLE MISSING_TABLE (\n%s\n)" srcMissingTableFields) |> ignore
    executeCmd (sprintf "CREATE TABLE DIFF_TABLE (\n%s\n)" srcDiffTableFields) |> ignore
    tx.Commit()
    connection.Close()
let runTrgDdl =
    use connection = new FbConnection(trgConnectionString)
    connection.Open()
    use tx = connection.BeginTransaction()
    let executeCmd text =
        printfn "%s" text
        let cmd = new FbCommand(text, connection, tx)
        cmd.CommandType <- Data.CommandType.Text
        cmd.ExecuteNonQuery()
    executeCmd (sprintf "CREATE TABLE DIFF_TABLE (\n%s\n)" trgDiffTableFields) |> ignore
    executeCmd (sprintf "CREATE TABLE TABLE_TO_DROP (\n%s\n)" trgTableToDropFields) |> ignore
    executeCmd ("ALTER TABLE DIFF_TABLE ADD CONSTRAINT FK_TO_DROP FOREIGN KEY (FK_ID) REFERENCES TABLE_TO_DROP (ID);") |> ignore
    tx.Commit()
    connection.Close()

runSrcDdl
runTrgDdl