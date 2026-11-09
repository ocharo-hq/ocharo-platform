using BrandWorkspace.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Meta;

// Answer a verb, or build, configure and serve the host. `import sourcing|events` and `media verify` run on the built host
// without serving; `migrate` and `config check` are the be sdk's own.
return await ServiceHost.RunAsync(
    args,
    builder => builder.Configure(),
    app => app.ConfigureAsync(),
    verbs => verbs.AddDeskVerbs());

public partial class Program;
