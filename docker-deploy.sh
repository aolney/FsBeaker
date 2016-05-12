printf "\n******************************************************************\n"
printf "This script will install the fsharp plugin for the Beaker docker container.\n\n"
echo -n "Do you want to install mono/fsharp? [Y]: "
printf "\n******************************************************************\n"
read -r -n 1 -s installMono
if [[ $installMono = [Yy] ]]; then
	apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
	echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list
	apt-get update
	apt-get install mono-complete fsharp dos2unix nano
	printf "\n******************************************************************\n"
	printf "Mono/fsharp installed"
	printf "\n******************************************************************\n"
else
	printf "\n---> Skipping mono/fsharp installation\n"
fi
printf "\n******************************************************************\n"
printf "Deploying fsbeaker plugin to docker container"
printf "\n******************************************************************\n"
cd /home/beaker
rm Release-0.3.2-alpha.zip 
rm -rf plugins/
cp /z/aolney/repos/FsBeaker/release/Release-0.3.2-alpha.zip ./
unzip Release-0.3.2-alpha.zip 
cd src/core/config/plugins/eval/
#mv fsharp fsharp-bk
rm -rf fsharp
cp -r ~/plugins/eval/fsharp/ ./
cd fsharp
find . -type f -exec dos2unix {} \;
cd ..
chown -R beaker:beaker fsharp
chmod -R 777 fsharp

printf "\n******************************************************************\n"
printf "MANUAL STEP: You must edit addevalplugins.js with the following code: \n\n \"FSharp\": { url : \"./plugins/eval/fsharp/fsharp.js\", bgColor: \"#378BBA\", fgColor: \"#FFFFFF\", borderColor: \"\", shortName: \"F#\" }\n"
printf "\nLAUNCHING EDITOR NOW. ABOVE LINE WILL DISAPPEAR SO COPY IT\n"
printf "\nDON'T FORGET TO PLACE COMMA AT END OF LINE ABOVE IN FILE IF NEEDED\n"
printf "\nREADY?\n"
read -r -n 1 -s
printf "\n******************************************************************\n"

cd ~/src/core/src/main/web/plugin/init
nano addevalplugins.js

printf "\n******************************************************************\n"
printf "Plugin deployed. If this works don't forget to tag this container, e.g. docker commit XXXXXX beakernotebook/beaker:fsharp"
printf "\n******************************************************************\n"
exit 1
