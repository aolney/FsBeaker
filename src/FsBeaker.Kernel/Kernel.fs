namespace FsBeaker.Kernel

open System
open System.IO
open System.Text
open Newtonsoft.Json
open System.Diagnostics
open System.Reflection
open System.Threading.Tasks
open System.Threading

[<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
type Table = {
    [<JsonProperty("columnNames")>]
    ColumnNames: string[]

    [<JsonProperty("values")>]
    Values: string[][]
}

[<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
type IntellisenseRequest = {
    [<JsonProperty("code")>]
    Code: string
    
    [<JsonProperty("lineIndex")>]
    LineIndex: int

    [<JsonProperty("charIndex")>]
    CharIndex: int
}

[<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
type IntellisenseResponse = {
    [<JsonProperty("declarations")>]
    Declarations: SimpleDeclaration[]

    [<JsonProperty("startIndex")>]
    StartIndex: int
}

[<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
type ExecuteRequest = {
    [<JsonProperty("code")>]
    Code: string
}

[<CLIMutable(); JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
type ExecuteResponse = {
    [<JsonProperty("result")>]
    Result: BinaryOutput

    [<JsonProperty("status")>]
    Status: ExecuteReponseStatus
}
and ExecuteReponseStatus = OK = 0 | Error = 1

[<JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
type ShellRequest =
    | Intellisense of IntellisenseRequest
    | Execute of ExecuteRequest
    | Interrupt

type ProcessingState =
    | Executing
    | Idle

///Helper to allow FsiEvaluationSession to be Interrupted.
///At this time the interrupt method on FsiEvaluationSession only works when FsiEvaluationSession.Run is used.
type AbortableExecute() =
    let mutable disposed = false
    let mutable state = Idle
    let mutable executeSignal = new AutoResetEvent(false)
    let mutable action = fun () -> ()
    let mutable abort = false
    let thread = 
        Threading.Thread
            (fun () ->
                let rec loop() =
                    try
                        executeSignal.WaitOne() |> ignore
                        action()
                        action <- fun () -> ()
                        state <- Idle
                        loop()
                    with
                    | :? ThreadAbortException as e when abort = true ->
                        executeSignal.Dispose()
                        executeSignal <- null
                    | e ->
                        action <- fun () -> ()
                        state <- Idle
                        loop ()
                loop())
    do 
        thread.Start()
    member x.State = state
    member x.Interrupt() = thread.Abort()
    ///Execute action iff state is idle
    member x.Execute(f) = 
        match state with
        | Idle ->
            state <- Executing
            action <- f
            executeSignal.Set() |> ignore
            true
        | _ -> false
    member x.Dispose() = 
        x.Dispose(true)
        System.GC.SuppressFinalize(x)
    member x.Dispose(disposing) = 
        if not disposed then
            if disposing then
                abort <- true
                thread.Abort()
            disposed <- true
    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module KernelInternals = 

    let separator = "##"

    /// Alias for reader.ReadLine()
    let readLine(reader: TextReader) = 
        reader.ReadLine()

    /// Keeps reading from the reader until "##" is encountered
    let readBlock(reader: TextReader) = 
        let sb = StringBuilder()
        let mutable line = readLine reader

        while line <> separator && line <> null do
            sb.AppendLine(line) |> ignore
            line <- readLine reader

        if line = null then 
            None 
        else 
            let bytes = Convert.FromBase64String(sb.ToString())
            let json = Encoding.UTF8.GetString(bytes)
            Some(json, sb.ToString())

    /// Serializes an object to a string
    let serialize(o) =
        let ser = JsonSerializer()
        let writer = new StringWriter()
        ser.Serialize(writer, o)
        writer.ToString()
        
/// The console kernel handles console requests and responds by sending
/// json data back to the console
type ConsoleKernel() =
   
    /// Wrapped thread for code execution
    let executionThread = new AbortableExecute()
    
    /// Gets the header code to prepend to all items
    let headerCode = 
        //surprised this works -- better to use AppDomain.CurrentDomain.BaseDirectory?
        let file = FileInfo(Assembly.GetEntryAssembly().Location)
        let dir = file.Directory.FullName
        let includeFile = Path.Combine(dir, "Include.fsx")
        File.ReadAllText(includeFile)

    /// Sends a line
    let sendLine(str:string) = 
        stdout.WriteLine(str)

    /// Sends an object with the separator
    let sendObj(o) = 
        let json = JsonConvert.SerializeObject(o)
        let bytes = Encoding.UTF8.GetBytes(json)
        let encodedJson = Convert.ToBase64String(bytes)
        sendLine <| encodedJson
        sendLine <| separator

    /// Evaluates the specified code
    let eval (code: string) =
        let consoleOut = System.Console.Out
        Console.SetOut outStream //capture output written to console during FSI eval
        try
            fsiEval.EvalInteraction(code)
            Console.SetOut consoleOut
            let error = sbErr.ToString()
            if String.IsNullOrWhiteSpace(error) then 

                // return results (not yet)
                let result = 
                    match GetLastExpression() with
                    | Some(it) -> 
                            
                        let secondaryType = 
                            match it.ReflectionValue with
                            | null -> typeof<obj>
                            | _ -> it.ReflectionValue.GetType()

                        let printer = Printers.findDisplayPrinter(it.ReflectionType, secondaryType)
                        let (_, callback) = printer
                        callback(it.ReflectionValue)

                    | None -> 
                            
                        { ContentType = "text/plain"; Data = "" }

                { Result = result; Status = ExecuteReponseStatus.OK }
            
            else

                { Result = { ContentType = "text/plain"; Data = sbErr.ToString() }; Status = ExecuteReponseStatus.Error }
        with
        | e ->
            Console.SetOut consoleOut
            { Result = { ContentType = "text/plain"; Data = e.Message + "\r\n" + sbErr.ToString()}; Status = ExecuteReponseStatus.Error }
            

    /// Processes a request to execute some code
    let processExecute(req: ExecuteRequest) =
        // clear errors and any output
        sbOut.Clear() |> ignore
        sbErr.Clear() |> ignore

        // evaluate
        let response = 
            try
                eval req.Code
            with ex -> 
                { Result = { ContentType = "text/plain"; Data = ex.Message + ": " + sbErr.ToString() }; Status = ExecuteReponseStatus.Error }

        sendObj response 

    /// Gets the intellisense information and sends it back
    let processIntellisense(req: IntellisenseRequest) =
        try
            let (decls, startIndex) = GetDeclarations(req.Code, req.LineIndex, req.CharIndex)
            sendObj { Declarations = decls; StartIndex = startIndex }
        with
            ex -> 
                sendObj { Declarations = [||]; StartIndex = req.CharIndex }

    /// Process commands
    let processCommands block = 
        let shellRequest = JsonConvert.DeserializeObject<ShellRequest>(block)
        match executionThread.State,shellRequest with
        | Idle, Intellisense(x) -> processIntellisense(x)
        //This fails on some test cases for unknown reasons; removing thread from execute
        //| Idle, Execute(x) -> executionThread.Execute(fun () -> processExecute(x)) |> ignore
        | Idle, Execute(x) ->  processExecute(x) |> ignore
        | Executing, Interrupt -> executionThread.Interrupt()
        | _ -> () //Ignore all other cases. Do not want any output here else we might disturb a client waiting for exec results.

    /// The main loop
    let rec loop() =
        let block = readBlock stdin
        match block with
        | Some (json, _) -> 
            processCommands json
            loop()
        | None ->
            Logging.logMessage( "kernel failed in loop(), readBlock is None ")
            executionThread.Dispose()
            failwith "Stream ended unexpectedly"

    /// Executes the header code and then carries on
    let start() = 
        ignore <| eval headerCode
        loop()

    // Start the kernel by looping forever
    member __.Start() = start()

/// API for sending commands to a ConsoleKernel
type ConsoleKernelClient(p: Process) = 

    let writeLock = obj()
    let readLock = obj()

    let reader = p.StandardOutput
    let writer = p.StandardInput

    /// Sends a line
    let sendLine(str:string) = 
        writer.WriteLine(str)
        writer.Flush()
        
    /// Sends on object to the process
    let sendObj (o : obj) =
        lock writeLock 
            (fun () ->
                let v = 
                    match o with 
                    | :? IntellisenseRequest as x -> Intellisense(x)
                    | :? ExecuteRequest as x -> Execute(x)
                    | :? ShellRequest as x -> x
                    | _ -> failwith "Invalid object to send"

                let json = JsonConvert.SerializeObject(v)
                let bytes = Encoding.UTF8.GetBytes(json)
                let encodedJson = Convert.ToBase64String(bytes)
                sendLine <| encodedJson
                sendLine <| separator)

    /// Sends an object to the process and blocks until something is sent back
    let sendAndGet(o:obj) =
        lock readLock 
            (fun () ->
                sendObj o
                readBlock reader)

    /// The process
    member __.Process = p
    
    /// Executes the specified code and returns the results
    member __.Execute(req: ExecuteRequest) =
        match sendAndGet req with
        | Some (returnJson, raw) -> JsonConvert.DeserializeObject<ExecuteResponse>(returnJson)
        | None -> failwith "Stream ended unexpectedly"

    /// Convenience method for executing a command
    member __.Execute(code) =
        __.Execute({ Code = code })

    /// Performs intellisense functionality
    member __.Intellisense(req: IntellisenseRequest) =
        match sendAndGet req with
        | Some (returnJson, raw) -> JsonConvert.DeserializeObject<IntellisenseResponse>(returnJson)
        | None -> failwith "Stream ended unexpectedly"

    /// Performs intellisense functionality
    member __.Intellisense(code, lineIndex, charIndex) =
        __.Intellisense({ Code = code; LineIndex = lineIndex; CharIndex = charIndex })

    /// Interrupt hosted FSI
    member __.Interrupt() = sendObj Interrupt

    /// IDisposable, disposes of the process    
    interface IDisposable with
        
        /// Dispose of the process
        member __.Dispose() = 
            try
                p.Kill()
                p.Dispose()
            with
            | ex -> Console.WriteLine("Error on dispose: {0}", ex.Message )

    /// Show the dispose method
    member __.Dispose() = (__ :> IDisposable).Dispose()

    /// Starts a new instance of FsBeaker.Kernel.exe
    static member StartNewProcess() =
        let procStart = ProcessStartInfo()
        match Environment.OSVersion.Platform with
        | PlatformID.MacOSX | PlatformID.Unix ->
            procStart.FileName <- "mono"
            procStart.Arguments <- Path.Combine( Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "FsBeaker.Kernel.exe" )
        | _ -> procStart.FileName <- "FsBeaker.Kernel.exe"
        //
        procStart.RedirectStandardError <- true
        procStart.RedirectStandardInput <- true
        procStart.RedirectStandardOutput <- true
        procStart.UseShellExecute <- false
        procStart.CreateNoWindow <- true
        new ConsoleKernelClient(Process.Start(procStart))