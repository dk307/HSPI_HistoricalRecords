// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly", Justification = "<Pending>", Scope = "type", Target = "~T:Hspi.HspiBase")]
[assembly: SuppressMessage("Major Code Smell", "S112:General exceptions should never be thrown", Justification = "<Pending>", Scope = "member", Target = "~P:Hspi.Utils.TimeAndValueIterator.Current")]
[assembly: SuppressMessage("Major Code Smell", "S112:General exceptions should never be thrown", Justification = "<Pending>", Scope = "member", Target = "~P:Hspi.Utils.TimeAndValueIterator.Next")]
[assembly: SuppressMessage("Minor Code Smell", "S3963:\"static\" fields should be initialized inline", Justification = "<Pending>", Scope = "member", Target = "~M:Hspi.Database.SqliteDatabaseCollector.#cctor")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:Hspi.PlugIn.HtmlForFunctionInterval(System.Object)~System.String")]