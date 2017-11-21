namespace LiteDB.FSharp

open System
open System.Globalization

open FSharp.Reflection
open Newtonsoft.Json
open LiteDB


/// Utilities to convert between BSON document and F# types
[<RequireQualifiedAccess>]
module Bson = 
    /// Returns the value of entry in the BsonDocument by it's key
    let read key (doc: BsonDocument) =
        doc.[key]

    /// Reads a property from a BsonDocument by it's key as a string
    let readStr key (doc: BsonDocument) = 
        doc.[key].AsString

    /// Reads a property from a BsonDocument by it's key and converts it to an integer
    let readInt key (doc: BsonDocument) = 
        doc.[key].AsString |> int

    /// Adds an entry to a `BsonDocument` given a key and a BsonValue
    let withKeyValue key value (doc: BsonDocument) = 
        doc.Add(key, value)
        doc

    /// Reads a field from a BsonDocument as DateTime
    let readDate (key: string) (doc: BsonDocument) = 
        doc.[key].AsDateTime

    /// Removes an entry (property) from a `BsonDocument` by the key of that property
    let removeEntryByKey (key:string) (doc: BsonDocument) = 
        doc.Remove(key) |> ignore
        doc

    let private fsharpJsonConverter = FSharpJsonConverter()
    let private converters : JsonConverter[] = [| fsharpJsonConverter |]
    /// Converts a typed entity (normally an F# record) to a BsonDocument. 
    /// Assuming there exists a field called `Id` or `id` of the record that will be mapped to `_id` in the BsonDocument, otherwise an exception is thrown.
    let serialize<'t> (entity: 't) = 
        let typeName = typeof<'t>.Name
        let json = JsonConvert.SerializeObject(entity, converters)
        let doc = LiteDB.JsonSerializer.Deserialize(json) |> unbox<LiteDB.BsonDocument>
        doc.Keys
        |> Seq.tryFind (fun key -> key = "Id" || key = "id")
        |> function
          | Some key -> 
             doc
             |> withKeyValue "_id" (read key doc) 
             |> removeEntryByKey key
          | None -> 
              let error = sprintf "Exected type %s to have a unique identifier property of 'Id' (exact name)" typeName
              failwith error

    /// Converts a BsonDocument to a typed entity given the document the type of the CLR entity.
    let deserializeByType (entity: BsonDocument) (entityType: Type) = 
            //Rewrite deserializeByType thus we can Reslove NestedId Corrently 
            //Just See DbRef NestedId Test
            let getKeyFieldName (entityType: Type)= 
              if FSharpType.IsRecord entityType 
              then FSharpType.GetRecordFields entityType 
                   |> Seq.tryFind (fun field -> field.Name = "Id" || field.Name = "id")
                   |> function | Some field -> field.Name
                               | None -> "Id"
              else "Id"   
            let rewriteAllKey (entity:BsonDocument)=    
                let xs=entity.RawValue.Keys|>List.ofSeq
                let rec rewriteKey (xs:string list)  (entity:BsonDocument) (entityType: Type) key=
                    match xs with 
                    |[]  -> ()
                    |y::ys -> 
                        let continueToNext()= rewriteKey ys entity entityType key 
                        match y,entity.RawValue.[y] with 
                        |"_id",id->
                            entity
                            |>withKeyValue key id
                            |>removeEntryByKey "_id"
                            |>ignore
                            continueToNext()
                        |_,(:?BsonDocument as bson) ->
                            let cEntityType=entityType.GetProperty(y).PropertyType
                            if FSharpType.IsUnion cEntityType then 
                               continueToNext()
                            else
                                let cKey=getKeyFieldName cEntityType
                                rewriteKey (bson.RawValue.Keys|>List.ofSeq) bson cEntityType cKey
                                continueToNext()
                        |_,(:?BsonArray as bsonArray)->
                            let c=entityType.GetProperty(y)
                            let collectionType=entityType.GetProperty(y).PropertyType
                            match collectionType.Name with
                            |"FSharpList`1"-> continueToNext()
                            |_->let cEntityType=entityType.GetProperty(y).PropertyType.GetElementType()
                                let cKey=getKeyFieldName cEntityType
                                bsonArray
                                |>Seq.iter(fun bson-> 
                                      match bson with 
                                      | :?BsonDocument as bson->
                                         rewriteKey (bson.RawValue.Keys|>List.ofSeq) bson cEntityType cKey
                                      | _ -> ())
                                continueToNext()
                        |_ ->continueToNext()
                rewriteKey xs entity entityType (getKeyFieldName entityType)
                entity

            rewriteAllKey entity 
            |>LiteDB.JsonSerializer.Serialize
            |>fun json -> JsonConvert.DeserializeObject(json, entityType, converters)

    let serializeField(any: obj) : BsonValue = 
        // Entity => Json => Bson
        let json = JsonConvert.SerializeObject(any, Formatting.None, converters);
        LiteDB.JsonSerializer.Deserialize(json);

    /// Deserializes a field of a BsonDocument to a typed entity
    let deserializeField<'t> (value: BsonValue) = 
        // Bson => Json => Entity<'t>
        let typeInfo = typeof<'t>
        value
        // Bson to Json
        |> LiteDB.JsonSerializer.Serialize
        // Json to 't
        |> fun json -> JsonConvert.DeserializeObject(json, typeInfo, converters)
        |> unbox<'t>
    /// Converts a BsonDocument to a typed entity given the document the type of the CLR entity.
    let deserialize<'t>(entity: BsonDocument) = 
        let typeInfo = typeof<'t>
        deserializeByType entity typeInfo
        |> unbox<'t>
