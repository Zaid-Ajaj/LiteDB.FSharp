module Tests.DBRef

open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp
open Tests.Types
open LiteDB.FSharp.Help


let useDatabase (f: LiteRepository -> unit) = 
    let mapper = FSharpBsonMapper()
    mapper.Entity<Order>().DbRef(toLinq(<@fun c->c.Company@>))|>ignore
    mapper.Entity<Order>().DbRef(toLinq(<@fun c->c.EOrders@>))|>ignore
    use memoryStream = new MemoryStream()
    use db = new LiteRepository(memoryStream, mapper)
    f db  
let dbRefTests =
  testList "dbRefTests" [
    testCase "CLIType DBRef Token Test" <| fun _ -> 
      useDatabase<| fun db->
        let company= {Id =1; Name ="test"}  
        let order={ Id =1;Company =company;EOrders=[]}
        db.Insert(company)|>ignore
        db.Insert(order)|>ignore
        db.Update({Id=1;Name="Hello"})|>ignore
        let m=db.Query<Order>().Include(toLinq(<@fun c->c.Company@>)).FirstOrDefault()
        Expect.equal m.Company.Name  "Hello" "CLIType DBRef Token Test Corrently"
    testCase "CLIType DBRef NestedId token Test" <| fun _ -> 
      useDatabase<|fun db->
        let company= {Id =1; Name ="test"}  
        let order={ Id =1;Company =company;EOrders=[]}
        db.Insert(company)|>ignore
        db.Insert(order)|>ignore
        let m=db.Query<Order>().Include(toLinq(<@fun c->c.Company@>)).FirstOrDefault()
        Expect.equal m.Company.Id  1 "CLIType DBRef NestedId token Test Corrently"    
    testCase "CLIType DBRef with List token Test" <| fun _ -> 
      useDatabase<| fun db->
        let e1= {Id=1; OrderNumRange="test1"}
        let e2= {Id=2; OrderNumRange="test2"}
        let order=
          { Id =1
            Company ={Id =1; Name ="test"} 
            EOrders=[e1;e2]}
        db.Insert<EOrder>([e1;e2])|>ignore
        db.Insert(order)|>ignore
        db.Update({Id=1;OrderNumRange="Hello"})|>ignore
        let m=db.Query<Order>().Include(toLinq(<@fun c->c.EOrders@>)).FirstOrDefault()
        Expect.equal m.EOrders.[0].OrderNumRange  "Hello" "CLIType DBRef with List token Test Corrently"
    testCase "CLIType DBRef with list NestedId token Test" <| fun _ -> 
      useDatabase <| fun db->
        let e1= {Id=1; OrderNumRange="test1"}
        let e2= {Id=2; OrderNumRange="test2"}
        let order=
          { Id =1
            Company ={Id =1; Name ="test"} 
            EOrders=[e1;e2]}
        db.Insert<EOrder>([e1;e2])|>ignore
        db.Insert(order)|>ignore
        let m=db.Query<Order>().Include(toLinq(<@fun c->c.EOrders@>)).FirstOrDefault()
        Expect.equal m.EOrders.[0].Id  1 "CLIType DBRef with list NestedId token Test Corrently"             
  ]
