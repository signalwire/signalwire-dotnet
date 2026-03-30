using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Tests for CLI argument parsing and URL auth extraction logic.
///
/// Since the CLI is a standalone dotnet-script file, we replicate its
/// parsing logic here to validate it under xUnit.
/// </summary>
public class CliTests
{
    // ==================================================================
    //  URL parsing
    // ==================================================================

    [Fact]
    public void ParseUrl_SimpleUrl()
    {
        var (baseUrl, user, pass) = ParseUrl("http://localhost:3000/webhook");
        Assert.Equal("http://localhost:3000/webhook", baseUrl);
        Assert.Null(user);
        Assert.Null(pass);
    }

    [Fact]
    public void ParseUrl_WithAuth()
    {
        var (baseUrl, user, pass) = ParseUrl("http://admin:secret@localhost:3000/path");
        Assert.Equal("admin", user);
        Assert.Equal("secret", pass);
        Assert.DoesNotContain("admin", baseUrl);
        Assert.DoesNotContain("secret", baseUrl);
        Assert.Contains("localhost:3000", baseUrl);
    }

    [Fact]
    public void ParseUrl_AuthWithSpecialChars()
    {
        // URL-encoded special chars in password: pass%40word => pass@word
        var (_, user, pass) = ParseUrl("http://user:pass%40word@localhost:3000");
        Assert.Equal("user", user);
        Assert.Equal("pass@word", pass);
    }

    [Fact]
    public void ParseUrl_HttpsUrl()
    {
        var (baseUrl, _, _) = ParseUrl("https://example.signalwire.com/api");
        Assert.StartsWith("https://", baseUrl);
        Assert.Contains("example.signalwire.com", baseUrl);
    }

    [Fact]
    public void ParseUrl_TrailingSlashTrimmed()
    {
        var (baseUrl, _, _) = ParseUrl("http://localhost:3000/");
        Assert.False(baseUrl.EndsWith('/'));
    }

    [Fact]
    public void ParseUrl_NoPort()
    {
        var (baseUrl, _, _) = ParseUrl("http://localhost");
        Assert.Contains("localhost", baseUrl);
    }

    // ==================================================================
    //  Param parsing
    // ==================================================================

    [Fact]
    public void ParseParams_SingleParam()
    {
        var parms = ParseParams(["--param", "name=John"]);
        Assert.Single(parms);
        Assert.Equal("John", parms["name"]);
    }

    [Fact]
    public void ParseParams_MultipleParams()
    {
        var parms = ParseParams(["--param", "a=1", "--param", "b=2", "--param", "c=3"]);
        Assert.Equal(3, parms.Count);
        Assert.Equal("1", parms["a"]);
        Assert.Equal("2", parms["b"]);
        Assert.Equal("3", parms["c"]);
    }

    [Fact]
    public void ParseParams_ValueWithEquals()
    {
        var parms = ParseParams(["--param", "query=x=1&y=2"]);
        Assert.Equal("x=1&y=2", parms["query"]);
    }

    [Fact]
    public void ParseParams_EmptyValue()
    {
        var parms = ParseParams(["--param", "key="]);
        Assert.Equal("", parms["key"]);
    }

    // ==================================================================
    //  Flag parsing
    // ==================================================================

    [Fact]
    public void ParseFlags_Help()
    {
        var opts = ParseFullArgs(["--help"]);
        Assert.True(opts.Help);
    }

    [Fact]
    public void ParseFlags_Verbose()
    {
        var opts = ParseFullArgs(["--verbose", "--url", "http://localhost:3000"]);
        Assert.True(opts.Verbose);
    }

    [Fact]
    public void ParseFlags_Raw()
    {
        var opts = ParseFullArgs(["--raw", "--url", "http://localhost:3000"]);
        Assert.True(opts.Raw);
    }

    [Fact]
    public void ParseFlags_DumpSwml()
    {
        var opts = ParseFullArgs(["--dump-swml", "--url", "http://localhost:3000"]);
        Assert.True(opts.DumpSwml);
    }

    [Fact]
    public void ParseFlags_ListTools()
    {
        var opts = ParseFullArgs(["--list-tools", "--url", "http://localhost:3000"]);
        Assert.True(opts.ListTools);
    }

    [Fact]
    public void ParseFlags_Exec()
    {
        var opts = ParseFullArgs(["--exec", "lookup", "--url", "http://localhost:3000"]);
        Assert.Equal("lookup", opts.Exec);
    }

    [Fact]
    public void ParseFlags_Combined()
    {
        var opts = ParseFullArgs([
            "--url", "http://user:pass@localhost:3000",
            "--exec", "my_tool",
            "--param", "key=val",
            "--raw",
            "--verbose",
        ]);

        Assert.True(opts.HasUrl);
        Assert.Equal("user", opts.AuthUser);
        Assert.Equal("pass", opts.AuthPassword);
        Assert.Equal("my_tool", opts.Exec);
        Assert.Equal("val", opts.Params["key"]);
        Assert.True(opts.Raw);
        Assert.True(opts.Verbose);
    }

    // ==================================================================
    //  Helpers (replicate the CLI parsing logic for testability)
    // ==================================================================

    private static (string BaseUrl, string? User, string? Pass) ParseUrl(string url)
    {
        try
        {
            var uri = new Uri(url);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                var user = Uri.UnescapeDataString(parts[0]);
                var pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";

                var builder = new UriBuilder(uri) { UserName = "", Password = "" };
                var baseUrl = builder.Uri.ToString().TrimEnd('/');
                return (baseUrl, user, pass);
            }

            return (url.TrimEnd('/'), null, null);
        }
        catch (UriFormatException)
        {
            return (url.TrimEnd('/'), null, null);
        }
    }

    private static Dictionary<string, string> ParseParams(string[] args)
    {
        var result = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--param" && i + 1 < args.Length)
            {
                var param = args[++i];
                var eqIdx = param.IndexOf('=');
                if (eqIdx > 0)
                {
                    result[param[..eqIdx]] = param[(eqIdx + 1)..];
                }
            }
        }
        return result;
    }

    private static CliTestOptions ParseFullArgs(string[] args)
    {
        var opts = new CliTestOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url":
                    if (i + 1 < args.Length)
                    {
                        var url = args[++i];
                        var (baseUrl, user, pass) = ParseUrl(url);
                        opts.BaseUrl = baseUrl;
                        opts.HasUrl = true;
                        opts.AuthUser = user;
                        opts.AuthPassword = pass;
                    }
                    break;
                case "--dump-swml":
                    opts.DumpSwml = true;
                    break;
                case "--list-tools":
                    opts.ListTools = true;
                    break;
                case "--exec":
                    if (i + 1 < args.Length)
                        opts.Exec = args[++i];
                    break;
                case "--param":
                    if (i + 1 < args.Length)
                    {
                        var param = args[++i];
                        var eqIdx = param.IndexOf('=');
                        if (eqIdx > 0)
                            opts.Params[param[..eqIdx]] = param[(eqIdx + 1)..];
                    }
                    break;
                case "--raw":
                    opts.Raw = true;
                    break;
                case "--verbose":
                    opts.Verbose = true;
                    break;
                case "--help":
                case "-h":
                    opts.Help = true;
                    break;
            }
        }
        return opts;
    }

    private class CliTestOptions
    {
        public string BaseUrl { get; set; } = "";
        public bool HasUrl { get; set; }
        public string? AuthUser { get; set; }
        public string? AuthPassword { get; set; }
        public bool DumpSwml { get; set; }
        public bool ListTools { get; set; }
        public string? Exec { get; set; }
        public Dictionary<string, string> Params { get; } = new();
        public bool Raw { get; set; }
        public bool Verbose { get; set; }
        public bool Help { get; set; }
    }
}
