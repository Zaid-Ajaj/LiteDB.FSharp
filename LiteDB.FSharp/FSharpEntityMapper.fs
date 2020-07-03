namespace LiteDB.FSharp

open LiteDB

/// Custom EntityMapper to allow injection of this libraries own deserialization
type FSharpEntityMapper(forType) as this =
    inherit EntityMapper(forType)

    do
        this.CreateInstance <-
            new CreateObject(Bson.deserializeByType forType)