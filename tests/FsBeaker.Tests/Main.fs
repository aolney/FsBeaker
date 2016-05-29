module FsBeaker.Tests.Main

open FsBeaker.Tests
open NUnit.Framework
open System.Text
open FsBeaker
open FsBeaker.Kernel

//NOTE: This is a quick/dirty way of explore a specific test case causing an error, as opposed to a general test case that should be a Nunit test
//In library mode (needed for Nunit), comment out entrypoint and change project type to library in fsproj
//In exe mode, uncomment entrypoint and change project type to exe
//[<EntryPoint>]
let main _ = 
    let code =
        StringBuilder()
            .AppendLine("#r @\"/z/aolney/shiloh/whistle/WhistleAPI/packages/Newtonsoft.Json.7.0.1/lib/net40/Newtonsoft.Json.dll\"")
            .AppendLine("let jsonFile = @\"/z/aolney/research_projects/class5/data/master-merged-data/nd-0316/140204TGT30307A.sent.ser.json\"")
            .AppendLine("type DiscourseTree = {
              kind : string
              relLabel : string
              relDir : string
              sentence : int
              firstToken : int
              lastToken : int
              text : string
              kids : DiscourseTree array
            }

            type Coreference = {
              sentence : int
              head : int
              start : int
              ``end`` : int
              chainLength : int
              chainId : int
              text : string
            }

            type Word = {
              token : string
              lemma : string
              tag : string
              entity : string
            }

            type Dependency = {
              sentence : int
              head : int
              dependent : int
              label : string
            }

            type CluResult = {
              discourse : DiscourseTree
              coreference : Coreference array
              word : Word array array
              dependencies: Dependency array array
            }")
            .AppendLine("let clu = Newtonsoft.Json.JsonConvert.DeserializeObject<CluResult>( System.IO.File.ReadAllText( jsonFile ) )")
            .ToString()
    
    use client = ConsoleKernelClient.StartNewProcess()
    let result = client.Execute(code)
    System.Console.Error.WriteLine( result.Result.Data.ToString() )
    0