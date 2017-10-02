namespace LiteDB.FSharp

open System
open FSharp.Reflection
open LiteDB
open Newtonsoft.Json

/// Utilities to convert between BSON document and F# types
module Bson = 
    let read key (doc: BsonDocument) =
        doc.[key]

    let addPair key value (doc: BsonDocument) = 
        doc.Add(key, value)
        doc

    let remove (key:string) (doc: BsonDocument) = 
        doc.Remove(key) |> ignore
        doc

    let private fableConverter = Fable.JsonConverter()
    let private converters : JsonConverter[] = [| fableConverter |]

    let serialize<'t> (entity: 't) = 
        let typeName = typeof<'t>.Name
        let json = JsonConvert.SerializeObject(entity, converters)
        let doc = LiteDB.JsonSerializer.Deserialize(json) |> unbox<BsonDocument>
        doc.Keys
        |> Seq.tryFind (fun key -> key = "Id" || key = "id")
        |> function
          | Some key -> 
             doc
             |> addPair "_id" (read key doc) 
             |> remove key
          | None -> 
              let error = sprintf "Exected type %s to have a unique identifier property of 'Id' (exact name)" typeName
              failwith error

    
    let deserializeByType (entity: BsonDocument) (entityType: Type) = 
        let key = 
          if FSharpType.IsRecord entityType 
          then FSharpType.GetRecordFields entityType 
               |> Seq.tryFind (fun field -> field.Name = "Id" || field.Name = "id")
               |> function | Some field -> field.Name
                           | None -> "Id"
          else "Id"
        entity
        |> addPair key (read "_id" entity) 
        |> remove "_id"
        |> LiteDB.JsonSerializer.Serialize // Bson to Json
        |> fun json -> JsonConvert.DeserializeObject(json, entityType, converters) // Json to obj
    
    let deserialize<'t>(entity: BsonDocument) = 
        let typeInfo = typeof<'t>
        deserializeByType entity typeInfo
        |> unbox<'t>
