﻿namespace Yard.Generators.CYKGenerator

open Brahma.Helpers
open OpenCL.Net
open Brahma.OpenCL
open Brahma.FSharp.OpenCL.Core
open Microsoft.FSharp.Quotations
open Brahma.FSharp.OpenCL.Extensions

open System.Collections.Generic


type GPUWork(extRowSize, extNTermsCount, extRecTable:_[], extRules, extRulesIndexed) =

    let rowSize = extRowSize
    let nTermsCount = extNTermsCount
    let mutable recTable = extRecTable
    let rules = extRules
    let rulesIndexed = extRulesIndexed
    // todo
    let localWorkSize = 1

    let platformName = "AMD*"
    let deviceType = DeviceType.Default
    
    // Configure provider
    // device provider creating
    let provider =
        try  ComputeProvider.Create(platformName, deviceType)
        with 
        | ex -> failwith ex.Message

    // command queue creating:
    let mutable commandQueue = new CommandQueue(provider, provider.Devices |> Seq.head)

    let run = 
        printfn "Using %A" provider
        
        // Write quotation
        let command = 
            <@
                fun (r:_3D) (rulesIndexed:_[]) -> 
                    let tx = r.GlobalID0 // len
                    let ty = r.GlobalID1 // i

                    let i = ty
                    let len = tx
                    
                    let chooseNewLabel ruleLabel (lbl1:byte) (lbl2:byte) lState1 lState2 =
                        if lState1 = LblState.Conflict then [| int noLbl; int LblState.Conflict |] // new LabelWithState(noLbl, LblState.Conflict)
                        elif lState2 = LblState.Conflict then [| int noLbl; int LblState.Conflict |] // new LabelWithState(noLbl, LblState.Conflict)
                        elif lState1 = LblState.Undefined && lState2 = LblState.Undefined && ruleLabel = noLbl then [| int noLbl; int LblState.Undefined |] // new LabelWithState(noLbl, LblState.Undefined)
                        else
                            let mutable notEmptyLbl1 = noLbl
                            let mutable notEmptyLbl2 = noLbl
                            let mutable notEmptyLbl3 = noLbl 
                            let mutable realLblCount = 0
                            if lbl1 <> noLbl then 
                                notEmptyLbl1 <- lbl1
                                realLblCount <- realLblCount + 1
                            if lbl2 <> noLbl then
                                if realLblCount = 0 then notEmptyLbl1 <- lbl2
                                elif realLblCount = 1 then notEmptyLbl2 <- lbl2
                                realLblCount <- realLblCount + 1
                            if ruleLabel <> noLbl then 
                                if realLblCount = 0 then notEmptyLbl1 <- ruleLabel
                                elif realLblCount = 1 then notEmptyLbl2 <- ruleLabel
                                elif realLblCount = 2 then notEmptyLbl3 <- ruleLabel
                                realLblCount <- realLblCount + 1

                            if realLblCount = 1 ||
                                (realLblCount = 2 && notEmptyLbl2 = notEmptyLbl1) ||
                                (realLblCount = 3 && notEmptyLbl2 = notEmptyLbl1 && notEmptyLbl3 = notEmptyLbl1)
                            then [| int noLbl; int LblState.Defined |] // new LabelWithState(notEmptyLbl1, LblState.Defined)
                            else [| int noLbl; int LblState.Conflict |] // new LabelWithState(noLbl, LblState.Conflict)
                    
                    let processRule rule ruleIndex i k l =
                        let rule = getRuleStruct rule
                        if rule.R2 <> 0us then
                            let leftStart = (i * rowSize + k) * nTermsCount
                            let rightStart = ((k+i+1) * rowSize + l-k-1) * nTermsCount

                            for m in 0..nTermsCount - 1 do
                                let leftCell:CellData = recTable.[leftStart + m]
                                if not (isCellDataEmpty (leftCell)) && getCellRuleTop leftCell rules = rule.R1 then
                                    let cellData1 = getCellDataStruct leftCell
                                    for n in 0..nTermsCount - 1 do
                                        let rightCell = recTable.[rightStart + n]
                                        if not (isCellDataEmpty (rightCell)) && getCellRuleTop rightCell rules = rule.R2 then
                                            let cellData2 = getCellDataStruct rightCell
                                            let lblWithState = chooseNewLabel rule.Label cellData1.Label cellData2.Label cellData1.LabelState cellData2.LabelState
                                            let newWeight = 0uy//weightCalcFun rule.Weight cellData1.Weight cellData2.Weight
                                            let currentElem = buildData ruleIndex (toState (lblWithState.[1])) (byte lblWithState.[0]) newWeight
                                            recTable.[(i * rowSize + l) * nTermsCount + int rule.RuleName - 1].rData <- currentElem
                                            recTable.[(i * rowSize + l) * nTermsCount + int rule.RuleName - 1]._k <- uint32 k
                                             // <- new CellData(currentElem, uint32 k) |> Some

                    for k in 0..(len-1) do
                        let curRule:RuleIndexed = rulesIndexed.[k]
                        let h = ref 0
                        h := 9//processRule curRule.Rule curRule.Index i k len
                    
            @>
    
        // Compile&Run
        // kernel function compilation
        let str = ref ""
        let kernel, kernelPrepare, kernelRun = provider.Compile(command,_outCode = str)
        printfn "%s" !str
        
        // computation grid configuration: 2D grid with size = rows*columns and local block with size=localWorkSize*localWorkSize
        let d =(new _3D(rules.Length,nTermsCount, nTermsCount, localWorkSize, localWorkSize, localWorkSize))
        // Prepare kernel. Pass actual parameters for computation:
        kernelPrepare d rulesIndexed
        // Add command into queue and finish it
        let _ = commandQueue.Add(kernelRun()).Finish()
    
        // Get result
        let _ = commandQueue.Add(recTable.ToHost(provider)).Finish()
        
        printfn "done."

    member this.Run() = run

    member this.Dispose() =
        // Releasing of resources
        commandQueue.Dispose()
        provider.Dispose()


type CYKOnGPU() =

    // правила грамматики, инициализируются в Recognize
    let mutable rules : array<rule> = [||]

    let mutable recTable:_[] = null

    let mutable rowSize = 0

    let mutable nTermsCount = 0

    let mutable lblNameArr = [||]

    let lblString lbl = 
        match lblNameArr with
        | [||] -> "0" 
        | _ -> 
                match lbl with 
                | 0uy -> "0"
                | _ -> lblNameArr.[(int lbl) - 1]
                
    let recognitionTable (_,_) (s:Microsoft.FSharp.Core.uint16[]) weightCalcFun =

        nTermsCount <- 
            rules
            |> Array.map(fun r -> 
                            let rule = getRuleStruct r
                            rule.RuleName)
            |> Set.ofArray
            |> Set.count

        rowSize <- s.Length
        recTable <- Array.init (rowSize * rowSize * nTermsCount) ( fun _ -> createEmptyCellData )

        (* writed in command *)
        (*
        let elem i len rulesIndexed = 
            // foreach rule r in grammar in parallel
            rulesIndexed 
            |> Array.iter (fun (curRule:RuleIndexed) -> ()(*for k in 0..(len-1) do processRule curRule.Rule curRule.Index i k len*))
        *)
        (*
        let elem2 i len symRuleArr = 
            // foreach symbol in grammar in parallel
            symRuleArr
            |> Array.Parallel.iter (fun (item:SymbolRuleMapItem) ->
                                        // foreach rule r per symbol in parallel
                                        item.Rules
                                        |> Array.iter (
                                            fun curRule -> ()(*for k in 0..(len-1) do processRule curRule.Rule curRule.Index i k len*)
                                        )
            )
        *)
        let fillTable rulesIndexed =
          [|1..rowSize - 1|]
          |> Array.iter (fun len ->
                printfn "iter 1"
                [|0..rowSize - 1 - len|] // for start = 0 to nWords - length in parallel
                |> Array(*.Parallel*).iter (fun i -> 
                    printfn "iter 2"
                    (new GPUWork(rowSize, nTermsCount, recTable, rules, rulesIndexed)).Run((* todo command exec *))(*elem i len rulesIndexed*))
                )
        (*
        let fillTable2 symRuleArr = 
            [|1..rowSize - 1|]
            |> Array.iter (fun len ->
                [|0..rowSize - 1 - len|] // for start = 0 to nWords - length in parallel
                |> Array.Parallel.iter (fun i -> elem2 i len symRuleArr))
        *)
        rules
        |> Array.iteri 
            (fun ruleIndex rule ->
                for k in 0..(rowSize - 1) do
                    let rule = getRuleStruct rule               
                    if rule.R2 = 0us && rule.R1 = s.[k] then
                        let lState =
                            match rule.Label with
                            | 0uy -> LblState.Undefined
                            | _   -> LblState.Defined
                        let currentElem = buildData ruleIndex lState rule.Label rule.Weight
                        recTable.[(k * rowSize + 0) * nTermsCount + int rule.RuleName - 1] <- new CellData(currentElem,0u) (*|> Some*))   
        //printfn "total rules count %d" rules.Length
                             
        let ntrIndexes = new ResizeArray<_>() // non-terminal rules indexes array
        rules
        |> Array.iteri
            (fun ruleIndex rule ->
                let ruleStruct = getRuleStruct rule
                if ruleStruct.R2 <> 0us then 
                    ntrIndexes.Add ruleIndex )
        let nonTermRules = Array.init ntrIndexes.Count (fun i -> new RuleIndexed(rules.[ntrIndexes.[i]], ntrIndexes.[i]) )        
        //printfn "non terminal rules count %d" nonTermRules.Length
        (*
        // left parts of non-terminal rules array
        // needed only for 2nd realization
        let symRuleMap = 
            nonTermRules
            |> Seq.groupBy (fun rule -> initSymbol (getRuleStruct rule.Rule).RuleName )
            |> Map.ofSeq
            |> Map.map (fun k v -> Array.ofSeq v)
        
        let symRuleArr =
            symRuleMap
            |> Map.toArray
            |> Array.map (fun (sym,rules) -> 
                            //printfn "Symbol %d rules count: %d" sym rules.Length
                            new SymbolRuleMapItem(sym,rules))
        *)
        let fillStart = System.DateTime.Now
        printfn "Fill table started %s" (string fillStart)
        fillTable nonTermRules
        let fillFinish = System.DateTime.Now
        printfn "Fill table finished %s [%s]" (string fillFinish) (string (fillFinish - fillStart))
        (*
        let fillImprStart = System.DateTime.Now
        printfn "Fill table improved started %s" (string fillImprStart)
        fillTable2 symRuleArr
        let fillImprFinish = System.DateTime.Now
        printfn "Fill table improved finished %s [%s]" (string fillImprFinish) (string (fillImprFinish - fillImprStart))
        *)
        recTable

    let recognize ((grules, start) as g) s weightCalcFun =
        let recTable = recognitionTable g s weightCalcFun
        
        let printTbl () =
            for i in 0..s.Length-1 do
                for j in 0..s.Length-1 do
                    let startIndex = (i * rowSize + j) * nTermsCount
                    let mutable count = 0
                    for m in startIndex..startIndex + nTermsCount - 1 do
                        if not (isCellDataEmpty (recTable.[m])) then count <- count + 1
                    printf "! %s !" (string count)
                printfn " "
            printfn "" 
            
        let getString state lbl weight = 
            let stateString = 
                match state with
                |LblState.Defined -> "defined"
                |LblState.Undefined -> "undefined"
                |LblState.Conflict -> "conflict"
                |_ -> ""

            String.concat " " [stateString; ":"; "label ="; lblString lbl; "weight ="; string weight]
            
        let rec out i last =
            let cellData = recTable.[(0 * rowSize + s.Length-1) * nTermsCount + i]
            if i <= last 
            then 
                if not (isCellDataEmpty (cellData))
                then
                    let cellData = getCellDataStruct (cellData)
                    if i = last
                    then [getString cellData.LabelState cellData.Label cellData.Weight]
                    else getString cellData.LabelState cellData.Label cellData.Weight :: out (i+1) last
                else "" :: out (i+1) last
            else [""]

        let lastIndex = nTermsCount - 1 //(recTable.[0 * rowSize + s.Length-1]).Length - 1
        
        out 0 lastIndex

    let print lblValue weight leftI rightL leftL =
        let out = String.concat " " ["label ="; lblString lblValue; "weight ="; string weight; 
                    "left ="; string leftI; "right ="; string (leftI + rightL + leftL + 1)]
        printfn "%s" out

    let rec trackLabel i l (cell:CellData)  flag =
        let ruleInd,_,curL,curW = getData cell.rData
        let rule = getRuleStruct rules.[int ruleInd]
        let (leftI,leftL),(rightI,rightL) = getSubsiteCoordinates i l (int cell._k)
        if l = 0
        then if curL <> noLbl
             then print curL curW leftI rightL leftL
        else 
            let checkIndex start ind tryFind ruleCheck =
                if ind < start + nTermsCount - 1 then
                    tryFind start (ind + 1) ruleCheck
                else None

            let rec tryFind start index ruleCheck = 
                let x = recTable.[index]
                match isCellDataEmpty x with
                | false ->
                    let ind,lSt,lbl,_ = getData x.rData
                    let curRule = getRuleStruct rules.[int ind]
                    if curRule.RuleName = ruleCheck then
                        Some (Some x)
                    else checkIndex start index tryFind ruleCheck
                | true -> checkIndex start index tryFind ruleCheck

            let startLeft = (leftI * rowSize + leftL) * nTermsCount
            let left = tryFind startLeft startLeft rule.R1

            let startRight = (rightI * rowSize + rightL) * nTermsCount
            let right = tryFind startRight startRight rule.R2
                
            match right with
            | Some (Some right) ->
                match left with 
                | Some (Some left) ->
                    let _,_,lLbl,_ = getData left.rData
                    let _,_,rLbl,_ = getData right.rData
                    if curL <> noLbl && lLbl = noLbl && rLbl = noLbl
                    then print curL curW leftI rightL leftL
                    else
                        trackLabel leftI leftL left true
                        trackLabel rightI rightL right true
                | _ -> ()
            | _ ->
                if flag && rule.Label <> noLbl
                then print curL curW leftI rightL leftL
            
    let labelTracking lastInd = 
        let i,l = 0,lastInd
        let startIndex = (i * rowSize + l) * nTermsCount
        for ind in startIndex..startIndex + nTermsCount - 1 do
            if not (isCellDataEmpty recTable.[ind]) // |> Option.iter(fun x ->
            then 
                let x = recTable.[ind]
                let out = "derivation #" + string (ind + 1)
                printfn "%s" out
                trackLabel i l x false //)
            
    
    member this.Recognize ((grules, start) as g) s weightCalcFun lblNames = 
        rules <- grules
        lblNameArr <- lblNames
        // Info about dialects of derivation in format: "<lblState> <lblName> <weight>"
        // If dialect undefined or was conflict lblName = "0" 
        let out = recognize g s weightCalcFun |> List.filter ((<>)"") |> String.concat "\n"
        match out with
        | "" -> "Строка не выводима в заданной грамматике."
        | _ -> 
            labelTracking (s.Length - 1)
            out