namespace LiteDB.FSharp

open System
open System.Globalization

open FSharp.Reflection
open Newtonsoft.Json
open LiteDB
open LiteDB


/// Utilities to convert between BSON document and F# types
[<RequireQualifiedAccess>]
module Bson = 
    /// Returns the value of entry in the BsonDocument by it's key
    let read (key: string) (doc: BsonDocument) =
        doc.[key]

    /// Reads a property from a BsonDocument by it's key as a string
    let readStr (key: string) (doc: BsonDocument) = 
        doc.[key].AsString

    /// Reads a property from a BsonDocument by it's key and converts it to an integer
    let readInt (key: string) (doc: BsonDocument) = 
        doc.[key].AsString |> int

    /// Reads a property from a BsonDocument by it's key and converts it to an integer
    let readBool (key: string) (doc: BsonDocument) = 
        doc.[key].AsString |> bool.Parse

    /// Adds an entry to a `BsonDocument` given a key and a BsonValue
    let withKeyValue (key: string) value (doc: BsonDocument) = 
        doc.Add(key, value)
        doc

    /// Reads a field from a BsonDocument as DateTime
    let readDate (key: string) (doc: BsonDocument) = 
        let date = doc.[key].AsDateTime
        if date.Kind = DateTimeKind.Local 
        then date.ToUniversalTime() 
        else date

    /// Removes an entry (property) from a `BsonDocument` by the key of that property
    let removeEntryByKey (key:string) (doc: BsonDocument) = 
        if (doc.ContainsKey key) 
        then doc.Remove(key) |> ignore
        doc

    let private fsharpJsonConverter = FSharpJsonConverter()
    let mutable internal converters : JsonConverter[] = [| fsharpJsonConverter |]
    
    /// Converts a typed entity (normally an F# record) to a BsonDocument. 
    /// Assuming there exists a field called `Id` or `id` of the record that will be mapped to `_id` in the BsonDocument, otherwise an exception is thrown.
    let serialize<'t> (entity: 't) = 
        let typeName = typeof<'t>.Name
        let json = JsonConvert.SerializeObject(entity, converters)
        let doc = LiteDB.JsonSerializer.Deserialize(json) |> unbox<LiteDB.BsonDocument>
        for key in doc.Keys do
            if key.EndsWith("@") 
            then doc.Remove(key) |> ignore

        doc.Keys 
        |> Seq.tryFind (fun key -> key = "Id" || key = "id" || key = "_id")
        |> function
            | Some key -> 
               doc
               |> withKeyValue "_id" (read key doc) 
               |> removeEntryByKey key
            | None -> 
              let error = sprintf "Expected type %s to have a unique identifier property of 'Id' or 'id' (exact name)" typeName
              failwith error

    /// Converts a BsonDocument to a typed entity given the document the type of the CLR entity.
    let deserializeByType (entity: BsonDocument) (entityType: Type) =
        let getCollectionElementType (collectionType:Type)=
            let typeNames = ["FSharpList`1";"IEnumerable`1";"List`"; "List`1"; "IList`1"; "FSharpOption`1"]
            let typeName = collectionType.Name
            if List.contains typeName typeNames then
                collectionType.GetGenericArguments().[0]
            else if collectionType.IsArray then
                collectionType.GetElementType()
            else failwithf "Could not extract element type from collection of type %s"  collectionType.FullName           
        
        let getKeyFieldName (entityType: Type)= 
          if FSharpType.IsRecord entityType 
          then FSharpType.GetRecordFields entityType 
               |> Seq.tryFind (fun field -> field.Name = "Id" || field.Name = "id")
               |> function | Some field -> field.Name
                           | None -> "Id"
          else "Id"
             
        let rewriteIdentityKeys (entity:BsonDocument)=    
            
            let rec rewriteKey (keys:string list) (entity:BsonDocument) (entityType: Type) key =
                match keys with 
                | []  -> ()
                | y :: ys -> 
                    let continueToNext() = rewriteKey ys entity entityType key 
                    match y, entity.[y] with 
                    // during deserialization, turn key-prop _id back into original Id or id
                    | "_id", id ->
                        entity
                        |> withKeyValue key id
                        |> removeEntryByKey "_id"
                        |> (ignore >> continueToNext)

                    | "$id", id ->
                        entity
                        |> withKeyValue key id
                        |> removeEntryByKey "$id"
                        |> (ignore >> continueToNext)

                    |_, (:? BsonDocument as bson) ->
                        // if property is nested record that resulted from DbRef then
                        // also re-write the transformed _id key property back to original Id or id
                        let propType = entityType.GetProperty(y).PropertyType
                        if FSharpType.IsRecord propType    
                        then rewriteKey (List.ofSeq bson.Keys) bson propType (getKeyFieldName propType)
                        continueToNext()

                    |_, (:? BsonArray as bsonArray) ->
                        // if property is BsonArray then loop through each element
                        // and if that element is a record, then re-write _id back to original
                        let collectionType = entityType.GetProperty(y).PropertyType
                        let elementType = getCollectionElementType collectionType
                        if FSharpType.IsRecord elementType then
                            let docKey = getKeyFieldName elementType
                            for bson in bsonArray do
                                if bson.IsDocument 
                                then
                                  let doc = bson.AsDocument
                                  let keys = List.ofSeq doc.Keys
                                  rewriteKey keys doc elementType docKey
                        
                        continueToNext()
                    |_ -> 
                        continueToNext()
            
            let keys = List.ofSeq entity.Keys
            rewriteKey keys entity entityType (getKeyFieldName entityType)
            entity

        rewriteIdentityKeys entity 
        |> LiteDB.JsonSerializer.Serialize
        |> fun json -> JsonConvert.DeserializeObject(json, entityType, converters)

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
        // if the type is already a BsonDocument, then do not deserialize, just return as is.
        if typeof<'t>.FullName = typeof<BsonDocument>.FullName
        then 
            entity |> unbox<'t>
        else
            let typeInfo = typeof<'t>
            deserializeByType entity typeInfo
            |> unbox<'t>