module Tests.DBRef

open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Experimental
open Tests.Types
open LiteDB.FSharp.Linq
open LiteDB.FSharp.Extensions

let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"

let useTypeShapeDatabase (f: LiteRepository -> unit) = 
    let mapper = TypeShapeMapper()
    mapper.DbRef<Order,_>(fun c -> c.Company)
    mapper.DbRef<Order,_>(fun c -> c.EOrders)
    use memoryStream = new MemoryStream()
    use db = new LiteRepository(memoryStream, mapper)
    f db
    
let useDatabase (f: LiteRepository -> unit) = 
    let mapper = FSharpBsonMapper()
    mapper.DbRef<Order,_>(fun c -> c.Company)
    mapper.DbRef<Order,_>(fun c -> c.EOrders)
    use memoryStream = new MemoryStream()
    use db = new LiteRepository(memoryStream, mapper)
    f db
    
let dbRefTests =
  testList "DBRef Tests"  [
  
    testCase "CLIType DBRef Token Test" <| fun _ -> 
      useDatabase <| fun db ->
        let company = { Id = 1; Name = "InitializedCompanyName"}  
        let order = { Id = 1; Company = company; EOrders = []}
        db 
        |> LiteRepository.insertItem company
        |> LiteRepository.insertItem order
        |> LiteRepository.updateItem<Company> { Id = 1; Name = "UpdatedCompanyName" }
        |> LiteRepository.query<Order> 
        |> LiteQueryable.expand (Expr.prop (fun o -> o.Company))
        |> LiteQueryable.first
        |> function
            | { Id = 1; 
               Company = {Id = 1; Name = "UpdatedCompanyName"}; 
               EOrders = []} -> pass()
            | _ -> fail()            
       
        
    testCase "CLIType DBRef token without include Test" <| fun _ -> 
      useDatabase <| fun db ->
        let company = {Id = 1; Name = "InitializedCompanyName"}  
        let order = { Id = 1; Company = company; EOrders = []}
        db.Insert(company)
        db.Insert(order)
        let m = db.Query<Order>().FirstOrDefault()
        Expect.equal m.Company.Id 1 "CLIType DBRef NestedId token Test Corrently"   
    
    testCase "CLIType DBRef NestedId token Test" <| fun _ -> 
      useDatabase <| fun db ->
        let company = {Id = 1; Name = "InitializedCompanyName"}  
        let order = { Id = 1; Company = company; EOrders = []}
        db.Insert(company)
        db.Insert(order)
        let m = db.Query<Order>().Include(convertExpr <@ fun c -> c.Company @> ).FirstOrDefault()
        Expect.equal m.Company.Id 1 "CLIType DBRef NestedId token Test Corrently"    
    
    
    testCase "CLIType DBRef with List token Test" <| fun _ -> 
      useDatabase <| fun db->
        let e1 = {Id = 1; OrderNumRange="test1"; Items = []}
        let e2 = {Id = 2; OrderNumRange="test2"; Items = []}
        let order =
          { Id = 1
            Company = { Id = 1; Name ="test"} 
            EOrders = [e1; e2] }
            
        db.Insert<EOrder>([e1;e2]) |> ignore
        db.Insert(order)
        db.Update({ Id = 1 ; OrderNumRange = "Hello"; Items = [] }) |> ignore
        let m = db.Query<Order>().Include(convertExpr <@ fun c -> c.EOrders @>).FirstOrDefault()
        Expect.equal m.EOrders.[0].OrderNumRange  "Hello" "CLIType DBRef with List token Test Corrently"
    
    
    testCase "CLIType DBRef with list NestedId token Test" <| fun _ -> 
      useDatabase <| fun db->
        let e1= {Id=1; OrderNumRange="test1"; Items = []}
        let e2= {Id=2; OrderNumRange="test2"; Items = []}
        let order=
          { Id = 1
            Company ={Id =1; Name ="test"} 
            EOrders =[e1;e2]}
        db.Insert<EOrder>([e1;e2]) |> ignore
        db.Insert(order)
        let m = db.Query<Order>().Include(convertExpr <@ fun c -> c.EOrders @>).FirstOrDefault()
        Expect.equal m.EOrders.[0].Id  1 "CLIType DBRef with list NestedId token Test Corrently"             
  ]
