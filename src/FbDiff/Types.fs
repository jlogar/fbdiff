namespace FbDiff

open FSharp.Data.Sql
open FSharp.Data.Sql.Common

module Types =
    let [<Literal>] connectionString = @"Data Source=localhost;initial catalog=" + __SOURCE_DIRECTORY__ + @"\src.fdb;user id=SYSDBA;password=masterkey;Dialect=3;"
    type HR = SqlDataProvider<Common.DatabaseProviderTypes.FIREBIRD, connectionString, OdbcQuote = OdbcQuoteCharacter.DOUBLE_QUOTES, UseOptionTypes = true>
    type FieldEntry = (HR.dataContext.``Dbo.RDB$RELATION_FIELDSEntity``*HR.dataContext.``Dbo.RDB$FIELDSEntity``)

    type Identifier =
        private | Identifier of string
        static member Create (raw:string option) = match raw with
                                                    | Some x -> (Identifier (x.Trim()))
                                                    | None -> failwith "fuck. the DB is invalid, we cannot go on like this"
        static member Create (raw:string) = Identifier (raw.Trim())
        member x.unbox = x |> fun (Identifier d) -> d
    type FieldType =
        Varchar of int16
        | Int
        | SmallInt
        | Timestamp
        | Date
        | Time
    type Field = {
        Name: Identifier
        Type: FieldType
        Nullable: bool
        }
    type Key = {
        Name: Identifier
        Fields: Field list
        }
    type ForeignKey = {
        Name: Identifier
        Fields: Field list
        RefTable: Identifier
        }
    type Table = {
            Name: Identifier
            Pk: Key
            Fks: ForeignKey list
            Fields: Field list
        }

    type DiffField = {
        SrcField: Field
        TrgField: Field
    }
    type TableDiff = {
        Table: Table
        MissingFields: Field list
        DiffFields: Result<DiffField, (DiffField*string)> list
    }
    type Diff = {
        MissingTables: Table list
        DifferingTables: TableDiff list
        Errors: (Identifier * (DiffField * string) seq)seq
    }
