﻿namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FsBeaker")>]
[<assembly: AssemblyProductAttribute("FsBeaker")>]
[<assembly: AssemblyDescriptionAttribute("F# implementation for beaker notebook")>]
[<assembly: AssemblyVersionAttribute("0.3.2")>]
[<assembly: AssemblyFileVersionAttribute("0.3.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.3.2"
    let [<Literal>] InformationalVersion = "0.3.2"
