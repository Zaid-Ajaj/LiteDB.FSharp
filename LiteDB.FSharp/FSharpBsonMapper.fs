namespace LiteDB.FSharp

open LiteDB

type FSharpBsonMapper() = 
    inherit LiteDB.BsonMapper()
    override self.ToObject(entityType: System.Type, entity: BsonDocument) = Bson.deserializeByType entity entityType 
    override self.ToObject<'t>(entity: BsonDocument) = Bson.deserialize<'t> entity
    override self.ToDocument<'t>(entity: 't) = Bson.serialize entity