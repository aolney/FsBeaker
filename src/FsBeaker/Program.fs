open System
open System.Diagnostics
open System.IO
open FsBeaker

[<EntryPoint>]
let main args = 

    // get the port from the args
    let port = 
        match args with
        | [| port |] ->
            match UInt16.TryParse(port) with
            | true, p -> Some p
            | false, _ -> None
        | _ -> Some 9000us

    match port with
    | Some (p) ->

        Server.start(p)
        0

    | None ->
        stderr.WriteLine("Invalid port '{0}', killing kernel and nginx", args.[0] )
        for p in Process.GetProcessesByName("FsBeaker.Kernel") do p.Kill()
        for p in Process.GetProcessesByName("nginx") do p.Kill()
        stderr.WriteLine("Launching '{0}'", args.[0] )
        let launcher = args.[0]
        let psi = ProcessStartInfo(launcher)
        psi.WorkingDirectory <- Path.GetDirectoryName(launcher)
        Process.Start(psi) |> ignore
        0