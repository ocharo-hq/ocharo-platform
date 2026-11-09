using OcharoCatalogue.Api.Catalogue.Core.Verbs;
using OcharoCatalogue.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Meta;

// Answer a verb (the be sdk's `config check` and `migrate`, and `import-sqlite`), or build, configure and serve the host.
return await ServiceHost.RunAsync(
    args,
    builder => builder.Configure(),
    app => app.ConfigureAsync(),
    verbs => verbs.AddHostVerb(LegacySqliteImportVerb.Name, LegacySqliteImportVerb.RunAsync));

public partial class Program;
