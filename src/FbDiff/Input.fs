namespace FbDiff

open Types

type IndexSource = { IndexName:Identifier; FieldName:Identifier; TableName:Identifier; ConstraintType:string; }
type FkSource = {
                                KeyName: Identifier
                                IndexName: Identifier
                                TableName: Identifier
                                FieldName: Identifier
                                FieldPosition: int16
                                RefTableName: Identifier
                                RefFieldName: Identifier
                                RefFieldPosition : int16
                                }

module Input =
    let mapToField ((rf, f): FieldEntry) =
        {
            Name = Identifier.Create rf.RdbFieldName
            //https://www.firebirdsql.org/file/documentation/reference_manuals/fblangref25-en/html/fblangref-appx04-fields.html
            Type = match f.RdbFieldType.Value with 
                                        | 8s -> Int
                                        | 37s -> Varchar f.RdbCharacterLength.Value
                                        | x -> failwithf "Unknown field type %d." x
            //checking for being null is not enough (although the docs imply that)
            Nullable = match rf.RdbNullFlag with
                        | (Some x) when x = 1s -> false
                        | _ -> true
        }
    let mapToPk (fields:Field seq) (indexFields: IndexSource seq) =
        let name = (Seq.head indexFields).IndexName
        let a = indexFields |> Seq.map (fun idx -> Seq.find (fun (f:Field)->f.Name = idx.FieldName) fields)
        {
            Name = name
            Fields = Seq.toList a
            }
    let mapToFks (fields: Field seq) (fkeys: FkSource seq) =
        let fkByName = fkeys |> Seq.groupBy (fun x -> (x.KeyName, x.RefTableName))
        fkByName |> Seq.map (fun ((fkName, refTableName), fkSourceFields) ->
                                let fkNames = fkSourceFields |> Seq.map (fun x->x.FieldName)
                                {
                                                                Name = fkName
                                                                Fields = fields|> Seq.filter (fun f->fkNames|>Seq.contains f.Name)|>Seq.toList
                                                                RefTable = refTableName
                                                                }
                        )
    let mapToTable indices fkeys (tableName, fields:FieldEntry seq) =
        let primaryIndices = indices
                            |> Seq.filter (fun ix -> ix.ConstraintType = "PRIMARY KEY")
                            |> Seq.groupBy (fun ix -> ix.TableName)
                            |> Map.ofSeq
        let fkIndices = fkeys
                            |> Seq.groupBy (fun ix -> ix.TableName)
                            |> Map.ofSeq
        let mappedFields = fields |> Seq.map mapToField |>Seq.toList
        {
            Name = tableName
            Pk = mapToPk mappedFields (primaryIndices.Item tableName)
            //Pk = {Name = Identifier.Create "abc"; Fields = List.empty}
            Fks = fkIndices.TryFind tableName |> Option.map (fun x-> mapToFks mappedFields x) |> function
                                                                                                    | Some x->x|>Seq.toList
                                                                                                    | None -> List.empty
            //Fks = List.empty
            Fields = mappedFields
        }

    let getTables (ctx: HR.dataContext) =
        let indices = query {
                        for ix in ctx.Dbo.RdbIndices do
                            join sg in ctx.Dbo.RdbIndexSegments on (ix.RdbIndexName = sg.RdbIndexName)
                            join rc in ctx.Dbo.RdbRelationConstraints on (ix.RdbIndexName = rc.RdbIndexName)
                            select {
                                IndexName = Identifier.Create ix.RdbIndexName
                                FieldName = Identifier.Create sg.RdbFieldName
                                TableName = Identifier.Create rc.RdbRelationName
                                ConstraintType = rc.RdbConstraintType.Value
                            }
                        } |> Seq.toList
        //use a seperate query for this because we join index_segments by index name, not by relation name 
        let fKeys = query {
                        for rc in ctx.Dbo.RdbRelationConstraints do
                            join sg in ctx.Dbo.RdbIndexSegments on (rc.RdbIndexName = sg.RdbIndexName)
                            join refc in ctx.Dbo.RdbRefConstraints on (rc.RdbConstraintName = refc.RdbConstraintName)
                            join rc1 in ctx.Dbo.RdbRelationConstraints on (refc.RdbConstNameUq = rc1.RdbConstraintName)
                            join sg1 in ctx.Dbo.RdbIndexSegments on (rc1.RdbIndexName = sg1.RdbIndexName)
                            select {
                                KeyName = Identifier.Create rc.RdbConstraintName
                                IndexName = Identifier.Create rc.RdbIndexName
                                TableName = Identifier.Create rc.RdbRelationName
                                FieldName = Identifier.Create sg.RdbFieldName
                                FieldPosition = match sg.RdbFieldPosition with
                                                    | Some x -> x
                                                    | None -> failwith (sprintf "null field position in %A for field %A" rc.RdbConstraintName sg.RdbFieldName)
                                RefTableName = Identifier.Create rc1.RdbRelationName
                                RefFieldName = Identifier.Create sg1.RdbFieldName
                                RefFieldPosition = match sg1.RdbFieldPosition with
                                                    | Some x -> x
                                                    | None -> failwith (sprintf "null field position in %A for field %A" rc.RdbConstraintName sg1.RdbFieldName)
                                }
                        } |> Seq.toList
        query { 
            for rf in ctx.Dbo.RdbRelationFields do
            join r in ctx.Dbo.RdbRelations on (rf.RdbRelationName = r.RdbRelationName)
            join f in ctx.Dbo.RdbFields on (rf.RdbFieldSource = f.RdbFieldName)
            where (r.RdbViewBlr.IsNone && (r.RdbSystemFlag.IsNone || r.RdbSystemFlag.Value = 0s))
            //sortBy (rf.RdbRelationName, rf.RdbFieldPosition)
            select (rf, f)
        }
                    |> Seq.toList
                    //|> Seq.filter (fun (rf,_)-> rf.RdbRelationName.IsSome)
                    |> List.groupBy (fun (rf,_)-> Identifier.Create rf.RdbRelationName)
                    |> List.map (mapToTable indices fKeys)
