#r "paket:
nuget Fake.Core.Target
nuget Fake.IO.FileSystem
//"

#load ".fake/build.fsx/intellisense.fsx"

open System.IO

open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.FileSystemOperators

let cwd = __SOURCE_DIRECTORY__
let dotnet = "dotnet"
let projectPath = cwd </> "LiteDB.FSharp"
let testsPath = cwd </> "LiteDB.FSharp.Tests"

let run workingDir fileName args =
    printfn "CWD: %s" workingDir
    let fileName, args =
        if Environment.isUnix
        then fileName, args
        else "cmd", ("/C " + fileName + " " + args)
    let exitCode =
        { 
            Program = fileName
            WorkingDir = workingDir
            CommandLine = args
            Args = []
        }
        |> Fake.Core.Process.shellExec

    if exitCode <> 0 then
        failwithf "'%s> %s %s' task failed" workingDir fileName args

Target.create "RunTests" <| fun _ ->
    "run LiteDB.FSharp.Tests.fsproj"
    |> run testsPath dotnet

Target.create "Clean" <| fun _ -> 
    [
        projectPath </> "bin"
        projectPath </> "obj"
        testsPath </> "bin"
        testsPath </> "obj"
    ]
    |> Shell.cleanDirs

Target.create "Build" <| fun _ ->
    "build -c Release LiteDB.FSharp.fsproj"
    |> run projectPath dotnet 

Target.create "PublishNuget" <| fun _ ->
    let projectPath = cwd </> "LiteDB.FSharp"
    "pack -c Release"
    |> run projectPath dotnet 
    let nugetKey =
        match Environment.environVarOrNone "NUGET_KEY" with
        | Some nugetKey -> nugetKey
        | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"
    let nupkg = Directory.GetFiles(projectPath </> "bin" </> "Release") |> Seq.head
    let pushCmd = sprintf "nuget push %s -s nuget.org -k %s" nupkg nugetKey
    run projectPath dotnet pushCmd

"Clean"
   ==> "Build"
   ==> "PublishNuget"

"Clean"
   ==> "RunTests"

Target.runOrDefault "RunTests"