#assume no mono exists
apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list
apt-get update
apt-get install mono-complete fsharp
#deploy built fsbeaker to docker container
cd /home/beaker
rm Release-0.3.2-alpha.zip 
rm -rf plugins/
cp /z/aolney/repos/FsBeaker/release/Release-0.3.2-alpha.zip ./
unzip Release-0.3.2-alpha.zip 
cd src/core/config/plugins/eval/
mv fsharp fsharp-bk
cp -r ~/plugins/eval/fsharp/ ./
cd fsharp
find . -type f -exec dos2unix {} \;
cd ..
chown -R beaker:beaker fsharp
chmod -R 777 fsharp
echo "Plugin deployed. If this works don't forget to tag this container, e.g. docker commit XXXXXX beakernotebook/beaker:fsharp"
#top -d .01 -b -c > ~/top.txt


