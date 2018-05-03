namespace LiteDB.FSharp

open LiteDB
open System.Linq.Expressions
open System

module Extensions =
    type LiteCollection<'t> with
        /// Tries to find a document using the Id of the document. 
        member collection.TryFindById(id: BsonValue) = 
            let result : 't = collection.FindById(id)
            match box result with
            | null -> None
            | _ -> Some result

        /// Tries to find a document using the given query
        member collection.TryFind (query: Query) = 
            let skipped = 0
            let limit = 1
            collection.Find(query, skipped, limit)
            |> Seq.tryHead
    
    
    type LiteRepository with 
        ///Create a new permanent index in all documents inside this collections if index not exists already.
        member this.EnsureIndex<'T1,'T2> (exp: Expression<Func<'T1,'T2>>) =
            this.Database.GetCollection<'T1>().EnsureIndex(exp,true) |> ignore

    [<RequireQualifiedAccess>]
    module LiteRepository =

        ///Insert an array of new documents into collection. Document Id must be a new value in collection. Can be set buffer size to commit at each N documents
        let insertItems<'a> (items: seq<'a>) (lr:LiteRepository) =
            lr.Insert<'a>(items) |> ignore 
            lr
            
        ///Insert a new document into collection. Document Id must be a new value in collection
        let insertItem<'a> (item: 'a) (lr:LiteRepository) =
            lr.Insert<'a>(item)
            lr

        ///Update a document into collection.
        let updateItem<'a> (item: 'a) (lr:LiteRepository) =
            if lr.Update<'a>(item) = false then failwithf "Failed updated item %A" item
            else
                lr
        ///Returns new instance of LiteQueryable that provides all method to query any entity inside collection. Use fluent API to apply filter/includes an than run any execute command, like ToList() or First()
        let query<'a> (lr:LiteRepository) =
            lr.Query<'a>()

    [<RequireQualifiedAccess>]
    type LiteQueryable =
        ///Include DBRef field in result query execution
        static member ``include`` (exp: Expression<Func<'a,'b>>) (query: LiteQueryable<'a>) =
            query.Include(exp)
       
       ///Include DBRef field in result query execution
        static member expand (exp: Expression<Func<'a,'b>>) (query: LiteQueryable<'a>) =
            query.Include(exp)
       
        static member first (query: LiteQueryable<'a>) =
            query.First()

        static member toList (query: LiteQueryable<'a>) =
            query.ToEnumerable() |> List.ofSeq

        ///Add new Query filter when query will be executed. This filter use database index
        static member where (exp: Expression<Func<'a,bool>>) (query: LiteQueryable<'a>) =
            query.Where exp

        static member find (exp: Expression<Func<'a,bool>>) (query: LiteQueryable<'a>) =
            query |> LiteQueryable.where exp |> LiteQueryable.first

