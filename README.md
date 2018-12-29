# What?
This is a fork of the [excellent F# plugin for Beaker Notebook](https://github.com/BayardRock/FsBeaker).

This fork focusses on functionality from Linux and the Docker container.

# I'm impatient
No problem, go ahead and [pull a Docker image with Beaker and the FSharp plugin installed](https://hub.docker.com/r/aolney/beaker/).

# Building and Installation
To build and install:
- Clone this repo
- Run build.sh
- [Install docker](https://docs.docker.com/engine/installation/)
- Get the [Beaker docker image](https://hub.docker.com/r/beakernotebook/beaker/)
- Run it with `docker run -v /z/aolney:/z/aolney -p 8800:8800 -t beakernotebook/beaker` where /z/aolney is a directory on your computer that will be mapped to docker
- Open up a bash on docker with `docker exec -it XXXXX bash` where XXXXX is given by `docker ps`
- In docker bash, run the `docker-deploy.sh` script in this repo using your mapped directory, e.g. from docker bash type `/z/aolney/FsBeaker/docker-deploy.sh`
- Follow the instructions of the script
- Test it
    - Open chrome to `https://127.0.0.1:8800`
    - Log in
    - Create an empty notebook
    - Start the F# plugin from the language manager
    - Insert an F# cell
    - Try it out. An assigned variable's value can be printed out simply by putting it on the last line of the cell, e.g. if `let x = 3` is the first line and `x` is the final line, Beaker will print `3`
- If everything worked, tag this container, e.g. `docker commit XXXXXX beakernotebook/beaker:fsharp`
- In the future, run this tagged image, e.g. `docker run -v /z/aolney:/z/aolney -p 8800:8800 -t beakernotebook/beaker:fsharp`

# Notes

Some features of the original plugin are currently disabled because of cross platform issues:
- Type providers
- FSharp.Charting

These were previously loaded automatically in include.fsx, called during plugin start up.

Core working features:
- Intellisense
- [Autotranslation](https://pub.beakernotebook.com/#/publications/7ae86a62-1b9f-11e6-9ac6-ff57a01b25df)

Here is an example of autotranslation using the `beaker` object (built in).

Assign 3 to x such that it can be called from other languages:

`beaker?x<-3`

Retrieve the value of x assigned in another language:

`beaker?x`

# Learn more about Beaker Notebooks
- [Wiki](https://github.com/twosigma/beaker-notebook/wiki)
- [Examples](https://pub.beakernotebook.com/#/publications/featured)
