open FsBeaker.Kernel
open System.IO

[<EntryPoint>]
let main _ = 

    Printers.addDefaultDisplayPrinters()

    try
        let kernel = ConsoleKernel()
        kernel.Start()
        0
    with 
        ex -> 
            let log(msg) =
                Logging.log(msg)
                stdout.WriteLine(msg) 
            
            log ("\n\nBegin Kernel Exception Log\n\n")

            let err = Evaluation.sbErr.ToString()
            let std = Evaluation.sbOut.ToString()

            log ("\nEvaluation ERR: " + err)
            log ("\nEvaluation STD: " + std)

            log ("\nStack trace:")
            log (ex.Message)
            log (ex.CompleteStackTrace())
            log ("")
            
            -1