rm -rf laml
git clone https://github.com/twilio/twilio-csharp.git laml
git apply --directory laml laml.patch
grep -Rl "\.twilio\.com" laml/* | xargs sed -i 's/.twilio.com/.signalwire.com/g'
