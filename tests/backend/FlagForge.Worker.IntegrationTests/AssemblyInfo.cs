using FlagForge.Testing;

// One SQL Server and one Redis container for the whole assembly; tests run sequentially and Respawn resets data.
[assembly: AssemblyFixture(typeof(FlagForgeFixture))]
