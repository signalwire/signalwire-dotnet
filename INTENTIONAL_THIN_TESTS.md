# INTENTIONAL_THIN_TESTS — methods exempt from the no-cheat-tests audit

**Currently empty** — there are no justified thin tests in this port.

The previous 2 entries were both `IDisposable.Dispose()` cleanup hooks on
shared xUnit fixtures (`MockServerFixture` / `RelayMockServerFixture`) — not
`[Fact]` tests. The auditor mis-flagged them because its `public void Name(...)`
shape pattern didn't require a test attribute. That detector bug is fixed
upstream — the auditor now only treats such a method as a test when a
`[Fact]`/`[Theory]`/`[TestMethod]`/etc. attribute precedes it, so `Dispose`
hooks and fixture constructors are never flagged and need no entry here.

For a genuine thin `[Fact]` that must stay, prefer the in-code marker over a
`file:line` entry (markers ride with the code through reflow; line numbers
drift):

<!-- snippet: no-compile test-illustration (xUnit [Fact] fragment; xUnit isn't referenced by the SDK build) -->
```csharp
[Fact]
public void SmokeConstructor() {  // no-cheat: smoke test — exercises the build path only
    _ = new Thing();
}
```

Format if a `file:line` entry is ever needed: `- <file:line> — <justification>`.
