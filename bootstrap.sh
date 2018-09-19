echo Bootstrapping...
rm -rf laml
git clone https://github.com/twilio/twilio-csharp.git laml
echo Patching...
sed 's/^M$//' laml.patch > laml.clean.patch
git apply --directory laml laml.clean.patch
rm -f laml.clean.patch
grep -Rl "\.twilio\.com" laml/src/* | xargs sed -i 's/.twilio.com/.signalwire.com/g'
grep -Rl "\.twilio\.com" laml/test/* | xargs sed -i 's/.twilio.com/.signalwire.com/g'
echo Done!
