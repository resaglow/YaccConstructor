﻿module CYKGeneratorTests

open Yard.Core
open Yard.Core.IL
open Yard.Core.IL.Production
open Yard.Core.IL.Definition
open Yard.Core.Checkers
open NUnit.Framework
open System.Linq
open System.IO

let generated = "Generated"

let filesAreEqual file1 file2 =
    let all1 = File.ReadAllBytes file1
    let all2 = File.ReadAllBytes file2
    Assert.AreEqual (all1.Length, all2.Length)
    Assert.IsTrue(Array.forall2 (=) all1 all2)

[<TestFixture>]
type ``CYK generator tests`` () =
    let generator = new Yard.Generators.CYKGenerator.CYKGeneartorImpl()
    let iGenerator = new Yard.Generators.CYKGenerator.CYKGenerator()
    let parser = new Yard.Frontends.YardFrontend.YardFrontend()
    let basePath = "../../../../Tests/CYK"

    [<Test>]
    member test.``Simple one rule without lable test`` () =        
        let il = parser.ParseGrammar(Path.Combine(basePath, "basic_noLBL.yrd"))
        let result = generator.GenRulesList il
        Assert.AreEqual(result.Length,1)
        Assert.AreEqual(result.[0], 281479271743488UL)

    [<Test>]
    member test.``Simple one rule with lable without weight test`` () = 
        let il = parser.ParseGrammar(Path.Combine(basePath, "basic_LBL_no_weight.yrd"))
        let result = generator.GenRulesList il
        Assert.AreEqual(result.Length,1)
        Assert.AreEqual(result.[0], 281479271743744UL)

    [<Test>]
    member test.``Simple one rule term with lable without weight test`` () =        
        let il = parser.ParseGrammar(Path.Combine(basePath, "basic_term_LBL_no_weight.yrd"))
        let result = generator.GenRulesList il
        Assert.AreEqual(result.Length,1)
        Assert.AreEqual(result.[0], 281479271678208UL)

    [<Test>]
    member test.``Simple one rule term without lable test`` () =        
        let il = parser.ParseGrammar(Path.Combine(basePath, "basic_term_noLBL.yrd"))        
        let result = generator.GenRulesList il
        Assert.AreEqual(result.Length,1)
        Assert.AreEqual(result.[0], 281479271677952UL)

    [<Test>]
    member test.``Simple one rule term without lable code gen test`` () =        
        let il = parser.ParseGrammar(Path.Combine(basePath, "basic_term_noLBL.yrd"))
        let expectedCode = 
            ["namespace Yard.Generators.CYK"
            ; ""
            ; "open Yard.Core"
            ; "type cykToken = "
            ; "  | NUM"
            ; "let getTag token = "
            ; "  match token with "
            ; "  | NUM -> 1"
            ; "let rules = "
            ; "  [ 281479271677952u ]"
            ; "  |> Array.ofList"
            ; "let CodeTokenStream (stream:seq<CYKToken<cykToken,_>>) = "
            ; "  stream |> Seq.map (fun t -> getTag t.Tag)"
            ] |> String.concat "\n"

        let code = generator.Generate il
        printfn "%s" expectedCode
        printfn "%s" "**************************"
        printfn "%s" code        
        Assert.AreEqual(expectedCode, code)

    [<Test>]
    member test.``Simple one rule term without lable code gen to file test`` () =
        let inFile = "basic_term_noLBL.yrd"
        let resultFile = inFile + ".CYK.fs"
        let inFullPath = Path.Combine(basePath, inFile)
        let resultFullPath = Path.Combine(basePath, resultFile)
        let expectedFullPath = Path.Combine [|basePath; generated; resultFile|]
        let il = parser.ParseGrammar inFullPath
        let code = iGenerator.Generate il
        System.IO.File.Exists resultFullPath |> Assert.IsTrue
        filesAreEqual resultFullPath expectedFullPath
        
