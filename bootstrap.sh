rm -rf laml
git clone https://github.com/twilio/twilio-csharp.git laml
sed 's/^M$//' laml.patch > laml.clean.patch
git apply --directory laml laml.clean.patch
rm -f laml.clean.patch
grep -Rl "\.twilio\.com" laml/* | xargs sed -i 's/.twilio.com/.signalwire.com/g'
