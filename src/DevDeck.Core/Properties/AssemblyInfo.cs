using System.Runtime.CompilerServices;

// The types here came across from V2 as `internal` and are left that way, so that syncing a fix
// from V2 stays a copy rather than a rewrite. The UI project is the only consumer.
//
// "DevDeck", not "DevDeck.App": this names the assembly, which the UI project renames to match the
// executable the user launches.
[assembly: InternalsVisibleTo("DevDeck")]

// The tests exercise these types directly. Everything worth testing in this project is internal,
// so without this the test project can only see an empty assembly.
[assembly: InternalsVisibleTo("DevDeck.Core.Tests")]
