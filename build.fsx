﻿#I @"packages/FAKE/tools"
#I @"packages/FAKE.BuildLib/lib/net451"
#r "FakeLib.dll"
#r "BuildLib.dll"

open Fake
open BuildLib

let solution = 
    initSolution
        "./Chatty.sln" "Release" [ ]

Target "Clean" <| fun _ -> cleanBin

Target "AssemblyInfo" <| fun _ -> generateAssemblyInfo solution

Target "Restore" <| fun _ -> restoreNugetPackages solution

Target "Build" <| fun _ -> buildSolution solution

Target "Test" <| fun _ -> testSolution solution

Target "Cover" <| fun _ ->
    coverSolutionWithParams 
        (fun p -> { p with Filter = "+[TalkServer*]* -[*.Tests]*" })
        solution

Target "Coverity" <| fun _ -> coveritySolution solution "SaladLab/Chatty"

Target "CI" <| fun _ -> ()

Target "DevLink" <| fun _ ->
    devlink "./packages" [ "../Akka.Interfaced"; "../Akka.Interfaced.SlimSocket"; "../Akka.Cluster.Utility" ]
    
Target "Help" <| fun _ -> 
    showUsage solution (fun _ -> None)

"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "Build"
  ==> "Test"

"Build" ==> "Cover"
"Restore" ==> "Coverity"

"Test" ==> "CI"
"Cover" ==> "CI"

RunTargetOrDefault "Help"
