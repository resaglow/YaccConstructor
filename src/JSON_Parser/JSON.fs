﻿module YC.ReSharper.AbstractAnalysis.Languages.JSON

open System.IO
open AbstractAnalysis.Common
open AbstractLexer.Core
open JSON.Parser
open System
open Mono.Addins

[<assembly:Addin>]
[<assembly:AddinDependency ("YC.ReSharper.AbstractAnalysis.Plugin.Core", "1.0")>]
do()

let parser = new Yard.Generators.RNGLR.AbstractParser.Parser<_>()

let tokenize lexerInputGraph =
    let eof = RNGLR_EOF("",[||])
    Lexer._fslex_tables.Tokenize(Lexer.fslex_actions_token, lexerInputGraph, eof)


let parse (*parser:Yard.Generators.RNGLR.AbstractParser.Parser<_>*) =
    
    fun parserInputGraph -> parser.Parse buildAstAbstract parserInputGraph

[<Extension>]
type JSONPars () =
    interface IInjectedLanguageProcessor<JSON.Parser.Token, JetBrains.ReSharper.Psi.CSharp.Tree.ICSharpLiteralExpression> with
        member this.Name = "JSON"
        member this.Parse (inG) = parse (inG)
        member this.NumToString (int) = JSON.Parser.numToString (int)
        member this.TokenData (token) = JSON.Parser.tokenData (token)
        member this.TokenToNumber (token) = JSON.Parser.tokenToNumber (token)
        member this.Tokenize (inG) = tokenize inG