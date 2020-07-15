echo Bootstrapping...
rm -rf laml
git clone --branch 5.39.1 https://github.com/twilio/twilio-csharp.git laml
echo Patching...
sed 's/^M$//' laml.patch > laml.clean.patch
git apply --directory laml laml.clean.patch
rm -f laml.clean.patch
echo Substituting...
grep -Rl "\.twilio\.com" laml/src/* | xargs sed -i 's/.twilio.com/.signalwire.com/g'
grep -Rl "\.twilio\.com" laml/test/* | xargs sed -i 's/.twilio.com/.signalwire.com/g'
echo Copying...
rm -rf signalwire-dotnet/laml
cp -R laml/src/Twilio signalwire-dotnet/laml
rm -rf signalwire-dotnet/laml/Properties signalwire-dotnet/laml/Twilio.csproj
rm -rf laml
echo Done!
