namespace FbDiff

open Types
open Util


module Output =
    let typeToStr fieldType = match fieldType with
                                | FieldType.Int -> "INTEGER"
                                | FieldType.SmallInt -> "SMALLINT"
                                | FieldType.Varchar l -> sprintf "VARCHAR(%d)" l
                                | FieldType.Date -> sprintf "DATE"
                                | FieldType.Time -> sprintf "TIME"
                                | FieldType.Timestamp -> sprintf "TIMESTAMP"
    let nullableToStr nullable = match nullable with
                                    | true -> ""
                                    | false -> " NOT NULL"
    let fieldToStr (field: Field) = sprintf "%s %s%s" (field.Name.unbox) (typeToStr field.Type) (nullableToStr field.Nullable)
    let mkFields (fields: Field list) = fields |> Seq.map fieldToStr |> mkstring ",\n"
    let mkPk (pk: Key) = sprintf "CONSTRAINT %s PRIMARY KEY (%s)" (pk.Name.unbox) (pk.Fields|>Seq.map (fun x -> x.Name.unbox) |> mkstring ",")
    let toCreateScript table =
        let strings = [
            sprintf "CREATE TABLE %s" (table.Name.unbox)
            sprintf "(%s,\n%s);" (mkFields table.Fields) (mkPk table.Pk)
            ]
        concatLines strings
    let toAlterField diffField = "ALTER " + diffField.SrcField.Name.unbox + "TYPE " + (typeToStr diffField.TrgField.Type)
    let toAlterScript tableDiff =
        let missingStrings = tableDiff.MissingFields |> List.map (fun x -> "ADD COLUMN " + (fieldToStr x)) |> mkstring ",\n"
        let diffStrings = tableDiff.DiffFields |> List.choose (function
                                                                        | Ok x -> Some x
                                                                        | Error _ -> None)
                                                |> List.map toAlterField
                                                |> mkstring ",\n"
        let strings = [
            sprintf "ALTER TABLE %s" (tableDiff.Table.Name.unbox)
            [missingStrings; diffStrings] |> mkstring ",\n"
            ";"
            ]
        concatLines strings
    let createScript (missingTables, differingTables) =
        let createScript = missingTables |> List.map toCreateScript
        let alterScript = differingTables |> List.map toAlterScript
        Seq.append createScript alterScript
    let getMissingTables src trg = src |> Seq.filter (fun s -> not (trg |> Seq.map (fun x->x.Name) |> Seq.contains s.Name))
    let getMissingFields (src, trg) =
        src.Fields |> List.filter (fun sf -> not (trg.Fields |> Seq.exists (fun tf->tf.Name = sf.Name)))
    let isSameField srcField (trgField:Field) = srcField = trgField
    let getDiffFields (srcFields: Field list) (trgFields: Field list) =
        printfn "srcFields %s" (srcFields|>Seq.map (fun x->x.Name.unbox)|>mkstring ",")
        printfn "trgFields %s" (trgFields|>Seq.map (fun x->x.Name.unbox)|>mkstring ",")
        //relying on equality here (src & target oughta have equality)
        let okFields = srcFields |> List.filter (fun sf -> Seq.contains sf trgFields)
        printfn "okFields %s" (okFields|>Seq.map (fun x->x.Name.unbox)|>mkstring ",")
        let diffFields = srcFields
                            |> List.except okFields
                            |> List.map (fun sf -> (sf, trgFields |> Seq.find (fun tf -> tf.Name = sf.Name)))
                            |> List.map (function
                                            | (sf,tf) when sf.Nullable<>tf.Nullable -> Error ({SrcField = sf; TrgField = tf}, "nullability change not supported")
                                            | (sf,tf) -> Ok {SrcField = sf; TrgField = tf}
                                            )
        diffFields
    let findByName s name = s |> Seq.find (fun x->x.Name = name)
    let compareTable (srcTable, trgTable) =
        printfn "comparing: %s" srcTable.Name.unbox
        let missing = getMissingFields (srcTable,trgTable)
        let diffFields = getDiffFields (srcTable.Fields |> List.except missing) trgTable.Fields
        //heh, need to get deprecated fields too, right? :)
        //let deprecatedFields =
        printfn "src FKs: %A" srcTable.Fks
        printfn "trg FKs: %A" trgTable.Fks
        let missingFks = srcTable.Fks |> Seq.except trgTable.Fks
        //let diffFks
        //let deprecatedFks =
        printfn "missing FKs: %A" missingFks
        {
            Table = trgTable
            MissingFields = missing
            DiffFields = diffFields
            }
    let compareTables src trg =
        let pairs = src |> Seq.map (fun st -> (st, (findByName trg st.Name)))
        printfn "pairs: %s" (pairs|> Seq.map (fun (t,_)->t.Name.unbox)|>mkstring ",")
        pairs |> Seq.map compareTable
    let takeWithError l = l|> Seq.choose (function | Ok _ -> None | Error (x, error) -> Some (x, error))
    let createDiff src trg =
        let missingTables = getMissingTables src trg |> Seq.toList
        let differingTables = compareTables (src |> Seq.except missingTables) trg |> Seq.toList
        let errors = differingTables
                    |> Seq.map (fun diff -> (diff.Table.Name, takeWithError diff.DiffFields))
                    |> Seq.filter (fun (_, fields) -> not (Seq.isEmpty fields))
        (missingTables, differingTables, errors)
