open FsBeaker.Kernel
open System.IO

[<EntryPoint>]
let main _ = 

    Printers.addDefaultDisplayPrinters()

    try
        let kernel = ConsoleKernel()
        Logging.logMessage( "starting kernel at " + System.DateTime.Now.ToLongTimeString() )
        kernel.Start()
        Logging.logMessage( "kernel quit unexpectedly at " + System.DateTime.Now.ToLongTimeString())
        0
    with 
        ex -> 
            let log(msg) =
                Logging.log(msg)
                stdout.WriteLine(msg) 

            let err = Evaluation.sbErr.ToString()
            let std = Evaluation.sbOut.ToString()

            log ("Evaluation ERR: " + err)
            log ("Evaluation STD: " + std)

            log ("Stack trace:")
            log (ex.Message)
            log (ex.CompleteStackTrace())
            log ("")
            
            -1