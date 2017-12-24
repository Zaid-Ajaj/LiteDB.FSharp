namespace LiteDB.FSharp

open LiteDB

module Extensions =
    type LiteCollection<'t> with
        /// Tries to find a document using the Id of the document. 
        member collection.TryFindById(id: BsonValue) = 
            let result : 't = collection.FindById(id)
            match box result with
            | null -> None
            | _ -> Some result