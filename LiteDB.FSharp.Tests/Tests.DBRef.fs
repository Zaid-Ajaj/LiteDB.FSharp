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

    testCase "CLIType DBRef token Test" <| fun _ -> 
      let defaultCompany=
        {Id =0
         Name ="test"}  
      let defaultOrder=
        { Id =0
          Company =defaultCompany}
      File.Delete("simple.db")|>ignore
      let mapper = FSharpBsonMapper()
      mapper.Entity<Order>().DbRef(toLinq(<@fun c->c.Company@>))|>ignore
      use db = new LiteRepository("simple.db",mapper)
      db.Insert(defaultCompany)|>ignore
      db.Insert(defaultOrder)|>ignore
      db.Update({defaultCompany with Name="Hello";Id=1})|>ignore
      let m=db.Query<Order>().Include(toLinq(<@fun c->c.Company@>)).FirstOrDefault().Company.Name
      Expect.equal m  "Hello" "CLIType DBRef token Test Corrently"
    testCase "CLIType DBRef NestedId token Test" <| fun _ -> 
      let defaultCompany=
        {Id =0
         Name ="test"}  
      let defaultOrder=
        { Id =0
          Company =defaultCompany}
      File.Delete("simple.db")|>ignore
      let mapper = FSharpBsonMapper()
      use db = new LiteRepository("simple.db",mapper)
      db.Insert(defaultCompany)|>ignore
      db.Insert(defaultOrder)|>ignore
      let m=db.Query<Order>().Include(toLinq(<@fun c->c.Company@>)).FirstOrDefault()
      Expect.equal m.Company.Id  1 "CLIType DBRef NestedId token Test Corrently"    
  ]