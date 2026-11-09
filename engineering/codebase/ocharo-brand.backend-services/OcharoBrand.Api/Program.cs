using OcharoBrand.Api.Brand.Import;
using OcharoBrand.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Meta;

// Answer a verb (`migrate` and `config check` are the be sdk's own, `import-sqlite` Brand's), or build, configure and
// serve the host.
return await ServiceHost.RunAsync(
    args,
    builder => builder.Configure(),
    app => app.ConfigureAsync(),
    verbs => verbs.AddHostVerb(LegacySqliteImportVerb.Name, LegacySqliteImportVerb.RunAsync));

public partial class Program;
