#r "packages/build/FAKE/tools/FakeLib.dll"

open Fake
open System
open System.IO


let cwd = __SOURCE_DIRECTORY__
let dotnet = "dotnet"
let projectPath = cwd </> "LiteDB.FSharp"


let run workingDir fileName args =
    printfn "CWD: %s" workingDir
    let fileName, args =
        if EnvironmentHelper.isUnix
        then fileName, args
        else "cmd", ("/C " + fileName + " " + args)
    let ok =
        execProcess (fun info ->
            info.FileName <- fileName
            info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if not ok then
        failwithf "'%s> %s %s' task failed" workingDir fileName args

Target "RunTests" <| fun () ->
    let testProjectPath = cwd </> "LiteDB.FSharp.Tests"
    "run LiteDB.FSharp.Tests.fsproj"
    |> run testProjectPath dotnet

Target "Clean" <| fun () -> 
    CleanDir (cwd </> projectPath </> "bin")
    CleanDir (cwd </> projectPath </> "obj")

Target "Build" <| fun () ->
    "build -c Release LiteDB.FSharp.fsproj"
    |> run projectPath dotnet 

Target "PublishNuget" <| fun () ->
    let projectPath = cwd </> "LiteDB.FSharp"
    "pack -c Release"
    |> run projectPath dotnet 
    let nugetKey =
        match environVarOrNone "NUGET_KEY" with
        | Some nugetKey -> nugetKey
        | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"
    let nupkg = Directory.GetFiles(projectPath </> "bin" </> "Release") |> Seq.head
    let pushCmd = sprintf "nuget push %s -s nuget.org -k %s" nupkg nugetKey
    run projectPath dotnet pushCmd

"Clean"
   ==> "Build" 
   ==> "PublishNuget" 

RunTargetOrDefault "RunTests"