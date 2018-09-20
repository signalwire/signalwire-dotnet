using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SignalWire
{
    public class LaunchSettingsFixture : IDisposable
    {
        public LaunchSettingsFixture()
        {
            using (StreamReader sr = File.OpenText(Path.Combine("Properties", "launchSettings.json")))
            {
                JObject.Load(new JsonTextReader(sr))
                    .GetValue("profiles")
                    .SelectMany(profiles => profiles.Children())
                    .SelectMany(profile => profile.Children<JProperty>())
                    .Where(prop => prop.Name == "environmentVariables")
                    .SelectMany(prop => prop.Value.Children<JProperty>())
                    .ToList()
                    .ForEach(p => Environment.SetEnvironmentVariable(p.Name, p.Value.ToString()));
            }
        }

        public void Dispose()
        {
        }
    }
}
