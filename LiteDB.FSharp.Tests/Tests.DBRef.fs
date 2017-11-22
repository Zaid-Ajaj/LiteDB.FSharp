module Tests.DBRef

open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp
open Tests.Types
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq.RuntimeHelpers
open System.Linq.Expressions


let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"
let toLinq (expr : Expr<'a -> 'b>) =
  let linq = LeafExpressionConverter.QuotationToExpression expr
  let call = linq :?> MethodCallExpression
  let lambda = call.Arguments.[0] :?> LambdaExpression
  Expression.Lambda<Func<'a, 'b>>(lambda.Body, lambda.Parameters) 
    
let dbRefTests =
  testList "dbRefTests" [

    testCase "CLIType DBRef Token Test" <| fun _ -> 
      let defaultCompany=
        {Id =0
         Name ="test"}  
      let defaultOrder=
        { Id =0
          Company =defaultCompany
          EOrders=[]}
      File.Delete("simple.db")|>ignore
      let mapper = FSharpBsonMapper()
      mapper.Entity<Order>().DbRef(toLinq(<@fun c->c.Company@>))|>ignore
      use db = new LiteRepository("simple.db",mapper)
      db.Insert(defaultCompany)|>ignore
      db.Insert(defaultOrder)|>ignore
      db.Update({defaultCompany with Name="Hello";Id=1})|>ignore
      let m=db.Query<Order>().Include(toLinq(<@fun c->c.Company@>)).FirstOrDefault().Company.Name
      Expect.equal m  "Hello" "CLIType DBRef Token Test Corrently"
    testCase "CLIType DBRef NestedId token Test" <| fun _ -> 
      let defaultCompany=
        {Id =0
         Name ="test"}  
      let defaultOrder=
        { Id =0
          Company =defaultCompany
          EOrders=[]}
      File.Delete("simple.db")|>ignore
      let mapper = FSharpBsonMapper()
      mapper.Entity<Order>().DbRef(toLinq(<@fun c->c.Company@>))|>ignore
      use db = new LiteRepository("simple.db",mapper)
      db.Insert(defaultCompany)|>ignore
      db.Insert(defaultOrder)|>ignore
      let m=db.Query<Order>().Include(toLinq(<@fun c->c.Company@>)).FirstOrDefault()
      Expect.equal m.Company.Id  1 "CLIType DBRef NestedId token Test Corrently"    
    testCase "CLIType DBRef with List token Test" <| fun _ -> 
      let defaultCompany=
        {Id =0
         Name ="test"}  
      let e1= {Id=0; OrderNumRange="test1"}
      let e2= {Id=0; OrderNumRange="test2"}
      let defaultOrder=
        { Id =0
          Company =defaultCompany
          EOrders=[e1;e2]}
      File.Delete("simple.db")|>ignore
      let mapper = FSharpBsonMapper()
      mapper.Entity<Order>().DbRef(toLinq(<@fun c->c.EOrders@>))|>ignore
      use db = new LiteRepository("simple.db",mapper)
      db.Insert<EOrder>([e1;e2])|>ignore
      db.Insert(defaultOrder)|>ignore
      db.Update({e1 with OrderNumRange="Hello";Id=1})|>ignore
      let m=db.Query<Order>().Include(toLinq(<@fun c->c.EOrders@>)).FirstOrDefault()
      Expect.equal m.EOrders.[0].OrderNumRange  "Hello" "CLIType DBRef with List token Test Corrently"
    testCase "CLIType DBRef with list NestedId token Test" <| fun _ -> 
      let defaultCompany=
        {Id =0
         Name ="test"}  
      let e1= {Id=0; OrderNumRange="test1"}
      let e2= {Id=0; OrderNumRange="test2"}
      let defaultOrder=
        { Id =0
          Company =defaultCompany
          EOrders=[e1;e2]}
      File.Delete("simple.db")|>ignore
      let mapper = FSharpBsonMapper()
      mapper.Entity<Order>().DbRef(toLinq(<@fun c->c.EOrders@>))|>ignore
      use db = new LiteRepository("simple.db",mapper)
      db.Insert<EOrder>([e1;e2])|>ignore
      db.Insert(defaultOrder)|>ignore
      let m=db.Query<Order>().Include(toLinq(<@fun c->c.EOrders@>)).FirstOrDefault()
      Expect.equal m.EOrders.[0].Id  1 "CLIType DBRef with list NestedId token Test Corrently travis-ciTest"             
  ]
