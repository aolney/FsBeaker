namespace FsBeaker

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Net

open FsBeaker.Kernel
open Newtonsoft.Json

open Suave
open Suave.Types
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.RequestErrors
open Suave.Http.Successful
open Suave.Http.Writers
open Suave.Web

module Server =

    [<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
    type EvaluateResponse = {
        [<JsonProperty("expression")>]
        Expression: string

        [<JsonProperty("status")>]
        Status: ExecuteReponseStatus

        [<JsonProperty("result")>]
        Result: BinaryOutput
    }

    [<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
    type EvaluateRequest = {
        ShellId: string
        Code: string
    }

    [<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
    type IntellisenseRequest = {
        ShellId: string
        Code: string
        LineIndex: int
        CharIndex: int
    }

    [<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
    type SetShellOptionsRequest = {
        ShellId: string
        SetupCode : string
        FsiArgs : string
    }

    let internal shells = ConcurrentDictionary<string, ConsoleKernelClient>()
    let internal findShell shellId = 
        let shell = shells.[shellId]
        if shell.Process.HasExited then
            failwithf "Kernel with shellId `%s` exited unexpectedly" shellId
        shell

    /// dispose shell and remove dictionary entry
    let internal killShell shellId = 
        let scc,v = shells.TryRemove(shellId)
        if scc then
            v.Dispose()
        scc

    /// Gets shell with the specified id if it exits otherwise a new shell is started and new id returned
    let internal getShell(shellId) = 
        if shells.ContainsKey(shellId) then
            shellId
        else
            let shellId = Guid.NewGuid().ToString()
            shells.GetOrAdd(shellId,(fun _ -> ConsoleKernelClient.StartNewProcess())) |> ignore
            shellId

    /// Starts the server on the specified port
    let start port =
    
        /// The configuration is the default listening to localhost:port.
        let config = {
            defaultConfig with
                bindings = [ HttpBinding.mk HTTP IPAddress.Loopback port ]
            }

        /// Scrubs the code of tabs and replaces them with four spaces
        let scrubCode(code:string) = code.Replace("\t", "    ")

        /// Requires that a parameter be present by name in the request. If the parameter
        /// is not in the request, then BAD_REQUEST is returned, otherwise the function is called
        /// with the parsed out parameter.
        let required (request : HttpRequest) parameterName f =
            match request.formData parameterName with
            | Choice2Of2 msg -> BAD_REQUEST("Parameter not supplied " + parameterName)
            | Choice1Of2 (v) -> f(v)

        /// Requires that a parameter be present by name in the request and be an integer. If the parameter
        /// is not in the request, then BAD_REQUEST is returned. If the parameter is not an integer, then 
        /// BAD_REQUEST is returned. If everything checks out, then the function is called with the parsed
        /// out integer
        let requiredInt request parameterName f =
            required request parameterName (fun v ->
                match Int32.TryParse(v) with
                | false, _ -> BAD_REQUEST("Expected integer for parameter " + parameterName)
                | true, v -> f(v)
            )

        /// Serializes the specified object into a JSON string
        let jsonOK o = JsonConvert.SerializeObject(o) |> OK
    
        /// The getShell API call
        let getShell r = 
            required r "shellId" (fun shellId ->
                OK <| getShell(shellId) //TODO: confirm that returning a new shellId if requested one is not available is the right thing to do.
            )

        /// The evaluate API call
        let evaluate(c: HttpContext) = 
            let r = c.request
            required r "shellId" (fun shellId ->
                required r "code" (fun code ->
                    let shell = findShell shellId
                    let res = shell.Execute code
                    {
                        Expression = code
                        Status = res.Status
                        Result = res.Result
                    }
                    |> jsonOK
                )
            )

        /// The intellisense API call
        let intellisense r =
            required r "shellId" (fun shellId ->
                required r "code" (fun code ->
                    requiredInt r "lineIndex" (fun lineIndex ->
                        requiredInt r "charIndex" (fun charIndex ->
                            try
                                let shell = findShell shellId
                                let newCode = scrubCode code
                                shell.Intellisense(newCode, lineIndex, charIndex) |> jsonOK
                            with
                                ex ->
                                    stderr.WriteLine(ex.Message)
                                    stderr.WriteLine(ex.StackTrace)
                                    { Declarations = [||]; StartIndex = 0 } |> jsonOK
                        )
                    )
                )
            )

        ///reset shellId
        let resetEnvironment (r : HttpRequest) = 
            let optionalString name = 
                match r.formData name with
                | Choice2Of2 _ -> ""
                | Choice1Of2 v -> v
            required r "shellId" (fun shellId ->
                let fsiArgs = optionalString "fsiArgs"
                if killShell shellId then
                    if shells.TryAdd(shellId, ConsoleKernelClient.StartNewProcess(fsiArgs)) then
                        OK <| "Shell reset"
                    else
                        OK <| "Could not recreate shell"
                else
                    OK <| "Could not find shell"
            )
            
        ///exit given shell id
        let exit r = 
            required r "shellId" 
                (fun shellId ->
                    killShell shellId |> ignore
                    OK shellId //TODO: not sure what the response should be
                )

        ///Interrupt hosted FSI given shellId
        let interrupt r =
            required r "shellId" 
                (fun shellId ->
                    let shell = findShell shellId
                    shell.Interrupt()
                    OK "" //TODO: not sure what the response should be
                )

        let app = 
            choose [
                POST >>= choose [
                    path "/fsharp/getShell"          >>= setHeader "Content-Type" "text/plain"       >>= request getShell
                    path "/fsharp/evaluate"          >>= setHeader "Content-Type" "application/json" >>= context evaluate
                    path "/fsharp/intellisense"      >>= setHeader "Content-Type" "application/json" >>= request intellisense
                    path "/fsharp/exit"              >>= setHeader "Content-Type" "text/plain"       >>= request exit
                    path "/fsharp/interrupt"         >>= setHeader "Content-Type" "text/plain"       >>= request interrupt
                    path "/fsharp/killAllThreads"    >>= OK "Not yet implemented"
                    path "/fsharp/resetEnvironment"  >>= setHeader "Content-Type" "text/plain" >>= request resetEnvironment
                    path "/fsharp/setShellOptions"   >>= setHeader "Content-Type" "text/plain" >>= request resetEnvironment
                ]
                GET >>= choose [
                    path "/fsharp/ready"             >>= OK "ok"
                ]
                NOT_FOUND "404"
            ]
            
        stdout.WriteLine("Successfully started server")
        startWebServer config app
        
