﻿module Yard.Generators.GLL.Parser 
open Yard.Generators.GLL 
open System 
open System.Collections.Generic
open Yard.Generators.GLL
open Yard.Generators.RNGLR
open Yard.Generators.RNGLR.AST
open Yard.Generators.RNGLR.DataStructures

type Label =  
    val Rule             : int
    val mutable Position : int 
    static member  Equal (label1 : Label) (label2 : Label) =
        let mutable result = true
        if label1.Rule <> label2.Rule || label1.Position <> label2.Position then result <- false
        result 
    new (rule : int,position : int) = {Rule = rule; Position = position} 

type IntermidiateNode =
    val LeftChild  : obj
    val RightChild : obj
    val Pos        : int * int
    val Extention  : int * int
    new (l , r, p, e) = {LeftChild = l; RightChild = r; Pos = p; Extention = e}

[<AllowNullLiteral>]
type Vertex  =
    val mutable OutEdges : UsualOne<Edge>
    val Level            : int
    val Value            : Label
    static member Equal (v1 : Vertex) (v2 : Vertex) =
        let mutable res = false
        if v1.Level = v2.Level && Label.Equal v1.Value v2.Value then
            res <- true
        res
    new (value, level) = {OutEdges = Unchecked.defaultof<_>; Value = value; Level = level}

and Edge =
    struct
        val Ast   : obj
        val Dest  : Vertex
        new (d, a) = {Dest = d; Ast = a}
    end

type Context =
    val Index         : int
    val Label         : Label
    val Node          : Vertex
    val Ast           : obj
    static member Equal (cntxt1 : Context) index label node (ast : obj) =
        let mutable res = false
        let t1, p1 =
            match cntxt1.Ast with
            //| :? Nodes as n -> Some(n.leftExt, n.rightExt)
            | :? AST as a -> Some(a.leftExt, a.rightExt), a.first.prod
            | :? IntermidiateNode as i -> Some i.Extention, fst i.Pos
            | _ -> None,0
        let t2, p2 =
            match ast with
            //| :? Nodes as n -> Some(n.leftExt, n.rightExt), n.
            | :? AST as a -> Some(a.leftExt, a.rightExt), a.first.prod
            | :? IntermidiateNode as i -> Some i.Extention, fst i.Pos
            | _ -> None,0
        if cntxt1.Index = index && Label.Equal cntxt1.Label label && Vertex.Equal cntxt1.Node node && (t1 = t2) && p1 = p2
            then 
                res <- true
        res    
    new (index, label, node, ast) = {Index = index; Label = label; Node = node; Ast = ast}

type ParseResult<'TokenType> =
    | Success of Tree<'TokenType>
    | Error of string
    


let buildAst<'TokenType> (parser : ParserSource2<'TokenType>) (tokens : seq<'TokenType>) : ParseResult<_> = 
    let tokens = Seq.toArray tokens
    let inputLength = Seq.length tokens
    let startNonTerm = parser.LeftSide.[parser.StartRule]
    let nonTermsCountLimit = 1 + (Array.max parser.LeftSide)
    let resultAST = ref None
    let getEpsilon =

         let epsilons = Array.init nonTermsCountLimit (fun i -> box (-i-1))
         fun i -> epsilons.[i]
    if inputLength = 0 || parser.IndexEOF = parser.TokenToNumber tokens.[0] then
        if parser.AcceptEmptyInput then
            Success (new Tree<_>(null, getEpsilon startNonTerm, null))
        else
            Error ("This grammar does not accept empty input.")     
    else
        
        let epsilon = new Nodes()
        let setR = new Queue<Context>();   
        let setP = new Queue<Vertex * obj>();    
        let setU = Array.init (inputLength + 1)  (fun _ -> new List<Label * Vertex * obj>())  
            
        let currentIndex = ref 0
        let previousIndex = ref 0

        let currentRule = parser.StartRule
        let dummy = box <| null
        let dummyGSSNode = new Vertex(new Label(currentRule, -1), !currentIndex)
        //let startGSSNode = new Vertex(new Label)
        let currentLabel = ref <| new Label(currentRule, 0)
        let startLabel = new Label(currentRule, 0)
        //let startGSSNode = new Vertex(!currentLabel, !currentIndex)
        
        let currentN = ref <| null
        let currentR = ref <| null

        //let currentGSSNode = ref <| new Vertex(!currentLabel, !currentIndex)
        let currentGSSNode = ref <| dummyGSSNode
        let currentContext = ref <| new Context(!currentIndex,!currentLabel,!currentGSSNode, dummy)
        
        
        let gss = Array.init inputLength (fun _ -> new ResizeArray<Vertex>())
        let ast = Array.init inputLength (fun _ -> new ResizeArray<AST>())


        let terminalNodes = new BlockResizeArray<Nodes>()

        let chainCanInferEpsilon rule pos =
            let curRule = parser.Rules.[rule]
            let mutable result = true
            for i = pos to curRule.Length - 1 do
                if result && not parser.canInferEpsilon.[curRule.[i]]
                then    
                    result <- false
            result

        let rec astToTokens (x : obj) =
            let mutable res = []
            match x : obj with 
            | :? int as t when t >= 0 -> res <- x :: res
            | :? Family as fam ->
                for i = 0 to fam.nodes.Length - 1 do
                    res <- res @ astToTokens fam.nodes.[i]
            | :? AST as ast ->
                if ast.other <> null 
                then
                    for family in ast.other do
                        if family.prod = parser.LeftSide.Length
                        then res <- res @ [ast]
                        else res <- res @ astToTokens family
                            
                if ast.first.prod = parser.LeftSide.Length
                then res <- [ast] @ res
                else res <- astToTokens ast.first @ res
            | _ -> ()
            res
        
        let handleIntermidiate node   (prod : int) = 
            let result = new List<obj>()
            let rec handle (o : obj) =
                if o <> null then
                        match o with
                        | :? IntermidiateNode as interNode ->
                            let t : IntermidiateNode = unbox interNode
                            handle t.LeftChild
                            handle t.RightChild
                        | :? Nodes as node -> 
                            result.Add (box <| node)     
                        | :? AST as ast    -> 
                             result.Add (box <| ast)
                        | _ -> failwith "Unexpected type."
                  
            handle node
            let res = result.ToArray()
            let l =
                match res.[0] with
                | :? Nodes as n ->
                    n.leftExt
                | :? AST as a -> a.leftExt
            let r =
                match res.[res.Length-1] with
                | :? Nodes as n ->
                    n.rightExt
                | :? AST as a -> a.rightExt
                    
           
            let fam = new Family(prod, new Nodes(res),l, r)
            fam
                                
        let containsContext (set : List<Label * Vertex * obj>) (label : Label) (gssNode : Vertex) (ast : obj) =
            let mutable result = false
            let first  (o, _, _) = o
            let second (_, o, _) = o
            let third  (_, _, o) = o
            for cntxt in set do    
                if (not result) 
                    && Label.Equal (first cntxt) label 
                    && Vertex.Equal (second cntxt) gssNode then
                    if ast = null && third cntxt = null 
                    then
                        result <- true
                    else
                        let t1, p1 =
                            match third cntxt with
                            //| :? Nodes as n -> Some(n.leftExt, n.rightExt)
                            | :? AST as a -> Some(a.leftExt, a.rightExt), a.first.prod
                            | :? IntermidiateNode as i -> Some i.Extention, fst i.Pos
                            | _ -> None,0
                        let t2, p2 =
                            match ast with
                            //| :? Nodes as n -> Some(n.leftExt, n.rightExt), n.
                            | :? AST as a -> Some(a.leftExt, a.rightExt), a.first.prod
                            | :? IntermidiateNode as i -> Some i.Extention, fst i.Pos
                            | _ -> None,0
                        if t1 = t2 && p1 = p2 then result <- true
                    
                                        
            result

        let addContext (label : Label)  (index : int) (node : Vertex) (ast : obj) =
            let res =
                if index <= inputLength && index >=0 then
                    if not <| containsContext setU.[index] label node ast
                    then
                        Some <| new Context(index, label, node, ast)
                    else None
                else None
            res
        
        let getNodeP (label : Label) (left : obj) (right : obj) =
            let mutable rExt =
               
                match right with
                | :? AST as a   -> Some(a.leftExt, a.rightExt)
                | :? Nodes as n -> Some(n.leftExt, n.rightExt) 
                | :? IntermidiateNode as i -> Some i.Extention
                | _ -> None //failwith "Unexpected type."
            
            let mutable result =  right
            if left <> null
            then
                let mutable lExt =    
                    match left with
                    | :? AST as a   -> (a.leftExt, a.rightExt)
                    | :? Nodes as n -> (n.leftExt, n.rightExt) 
                    | :? IntermidiateNode as i ->  i.Extention
                    | _ -> failwith "Unexpected type."
            
                result <- new IntermidiateNode(left, right, (label.Rule, label.Position), (fst lExt, (if rExt.IsNone then lExt else rExt.Value)|> snd)) 
            else 
                result <- new IntermidiateNode(left, right, (label.Rule, label.Position), (fst rExt.Value, snd rExt.Value))
            box <| result
                
            
        let getNodeT term b =
            if term.Equals epsilon
            then
                box <| new Nodes(term, null, null, b, b)
            else
                box <| new Nodes(term, null, null, b, b + 1)

        let containsGSSNode (l : Label) (i : int) =  
            let curLevel = gss.[i] 
            let mutable cond = true
            let mutable res = dummyGSSNode  
            for vrtx in curLevel do
                if cond && Label.Equal vrtx.Value l
                then 
                    res <- vrtx
                    cond <-false
            if cond
            then
                res <- new Vertex(l, i)     
            res

        let containsEdge (b : Vertex) (e : Vertex) (ast : obj)=
            let edges = b.OutEdges
            let mutable result = false
            if edges.first <> Unchecked.defaultof<_>
            then
                if not (Vertex.Equal edges.first.Dest e && edges.first.Ast.Equals ast)
                then
                    if edges.other <> null
                    then
                        for edge in edges.other do
                            if Vertex.Equal edge.Dest e && edge.Ast.Equals ast
                            then
                                result <- true
                else result <- true
            result                        

        let create (label : Label) (u : Vertex) (index : int) (ast : obj) = 
            let v = containsGSSNode label index
            if not (containsEdge v u ast)
            then
                let newEdge = new Edge(u, ast)
                for pair in setP do
                    if Vertex.Equal v (fst pair) 
                    then 
                        let y = getNodeP label ast (snd pair)
                        let temp : AST = unbox <| snd pair
                        let cntxt = addContext label temp.rightExt u y
                        if  cntxt.IsSome
                        then
                            setU.[temp.rightExt].Add (label, u, y)
                            setR.Enqueue(cntxt.Value)    
                if v.OutEdges.first <> Unchecked.defaultof<_>
                then
                    if v.OutEdges.other <> null
                    then
                        v.OutEdges.other <- Array.append v.OutEdges.other [|newEdge|]
                    else 
                        v.OutEdges.other <- [|newEdge|]
                else v.OutEdges.first <- newEdge
            gss.[index].Add v
            v
          
        let pop (u : Vertex) (i : int) (z : obj) prod =
            if not (Vertex.Equal u dummyGSSNode) then
                let label = u.Value
                setP.Enqueue(u, z)
                let processEdge (edge : Edge) =
                    if edge.Ast <> null && z <> null 
                    then 
                        printfn "fff"
                    let y1 = getNodeP label edge.Ast z
//                    let y2 = handleIntermidiate y1 prod
//                    let y = new AST(y2, null, y2.leftExt, y2.rightExt)
                    let cntxt = addContext label i edge.Dest y1
                    if  cntxt.IsSome
                    then
                        setU.[i].Add (label, edge.Dest, box y1)
                        setR.Enqueue(cntxt.Value)    
                processEdge u.OutEdges.first
                if u.OutEdges.other <> null then 
                    for edge in u.OutEdges.other do
                        processEdge edge

        let table = parser.Table
   
        let condition = ref false 
        let stop = ref false
   
        let rec dispatcher () =  
         
            if setR.Count <> 0 then
                currentContext := setR.Dequeue()
                currentIndex := currentContext.Value.Index
                
                if !currentIndex <> !previousIndex 
                then
                    previousIndex := !currentIndex 
                currentGSSNode := currentContext.Value.Node
                currentLabel := currentContext.Value.Label
                currentN := currentContext.Value.Ast 
                currentR := null
                condition := false
//                if !currentIndex >= inputLength 
//                then
//                    condition := true
            else 
                stop := true  
                
                printfn  "%A" setP
                let v,ast =
                    setP
                    |> Seq.find 
                        (fun (x,y) -> 
                            let ast = unbox y :> AST
                            x.Value.Rule = parser.StartRule && ast.leftExt = 0 && ast.rightExt = inputLength)
                resultAST := Some ast 
                              
        and processing () =  

            let getIndex(nTerm, term) = 
                let mutable index = nTerm
                index <- (index * (parser.IndexatorFullCount - parser.NonTermCount))
                index <- index + term - parser.NonTermCount
                index

            if Array.length parser.Rules.[currentLabel.Value.Rule] = 0 
            then
                currentR := getNodeT epsilon !currentIndex
                currentN := getNodeP !currentLabel !currentN !currentR  
                //pop !currentGSSNode !currentIndex !currentN
                condition := true
            else
                if Array.length parser.Rules.[currentLabel.Value.Rule]  <> currentLabel.Value.Position && !currentIndex < inputLength
                then
                    let curToken = parser.TokenToNumber tokens.[!currentIndex]
                    let curSymbol = parser.Rules.[currentLabel.Value.Rule].[currentLabel.Value.Position]
                    if parser.NumIsTerminal curSymbol  || parser.NumIsLiteral curSymbol 
                    then
                        if curSymbol = curToken
                        then
                            if !currentN = null 
                            then
                                if (terminalNodes.Item !currentIndex).fst = null
                                then
                                    currentN := getNodeT (box <| tokens.[!currentIndex]) !currentIndex
                                    terminalNodes.Set !currentIndex (unbox <| !currentN)
                                else 
                                    currentN := box <| terminalNodes.Item !currentIndex
                            else
                                if (terminalNodes.Item !currentIndex).fst = null
                                then
                                    currentR := getNodeT (box <| tokens.[!currentIndex]) !currentIndex
                                    terminalNodes.Set !currentIndex (unbox <| !currentR)
                                else 
                                    currentR := box <| terminalNodes.Item !currentIndex
                            currentIndex := !currentIndex + 1
                            currentLabel.Value.Position <- currentLabel.Value.Position + 1
//                            if !currentR <> null
//                            then
                            currentN := getNodeP !currentLabel !currentN !currentR
                            condition := false
                        else 
                            condition := true

                    else 
                        let index = getIndex(curSymbol, curToken)
                        let temp = table.[index]
                        if Array.length table.[index] <> 0 
                        then
                            currentGSSNode := create (new Label(currentLabel.Value.Rule, currentLabel.Value.Position + 1)) !currentGSSNode !currentIndex !currentN
                            for ruleN in table.[index] do
                                let newLabel = new Label(ruleN, 0)
                                let cntxt = addContext newLabel !currentIndex !currentGSSNode dummy
                                if  cntxt.IsSome
                                then
                                    setU.[!currentIndex].Add (newLabel, !currentGSSNode, dummy)
                                    setR.Enqueue(cntxt.Value)    
                            
                            condition := true
                           
                else 
//                    if currentLabel.Value.Rule = parser.StartRule && !currentIndex = inputLength
//                    then
//                        let curRight = unbox <| !currentN
//                        let resTree = handleIntermidiate curRight currentLabel.Value.Rule
//                        if (!resultAST).IsNone
//                        then
//                            
//                     //   let resTree = findTree resTree currentGSSNode.Value.Level currentLabel.Value.Rule
//                        //    let resTree = new AST(resTree, null)
//                            resultAST := Some <| new AST(resTree, null)
//                            condition := true
//
//                        else 
//                            let temp = (!resultAST).Value
//                            if temp.other <> null
//                            then
//                                temp.other <- Array.append temp.other [|resTree|]
//                            else
//                                temp.other <- [|resTree|]
//                        condition := true
//                    else
                    let curRight = unbox <| !currentN
                    let resTree = handleIntermidiate curRight currentLabel.Value.Rule
                    //   let resTree = findTree resTree currentGSSNode.Value.Level currentLabel.Value.Rule
                    let resTree = new AST(resTree, null, resTree.leftExt, resTree.rightExt)
                    pop !currentGSSNode !currentIndex resTree currentLabel.Value.Rule
                    condition := true
        let control () =
            while not !stop do
                if !condition then dispatcher() else processing()
        control()
             
       
        match !resultAST with
            | None -> Error ("String was not parsed")
            | Some res -> 
                    Success (new Tree<_> (tokens, res, parser.Rules))
  
         