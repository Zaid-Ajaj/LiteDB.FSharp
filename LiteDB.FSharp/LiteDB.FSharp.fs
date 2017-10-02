namespace LiteDB.FSharp

open System
open LiteDB
open Newtonsoft.Json
open Newtonsoft.Json.Linq

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
        |> Seq.tryFind (fun key -> key = "Id")
        |> function
          | Some key -> 
             doc
             |> addPair "_id" (read "Id" doc) 
             |> remove key
          | None -> 
              let error = sprintf "Exected type %s to have a unique identifier property of 'Id' (exact name)" typeName
              failwith error

    let deserialize<'t>(entity: BsonDocument) = 
        entity
        |> addPair "Id" (read "_id" entity) 
        |> remove "_id"
        |> LiteDB.JsonSerializer.Serialize // Bson to Json
        |> fun json -> JsonConvert.DeserializeObject<'t>(json, converters) // Json to 't


type FSharpBsonMapper() = 
    inherit LiteDB.BsonMapper()
    override self.ToObject<'t>(entity: BsonDocument) = Bson.deserialize<'t> entity
    override self.ToDocument<'t>(entity: 't) = Bson.serialize entity