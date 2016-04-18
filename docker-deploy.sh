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
#top -d .01 -b -c > ~/top.txt


