using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// TODO: Register services here
// builder.Services.AddSingleton<IIndexingCoordinator, IndexingCoordinator>();
// builder.Services.AddSingleton<IUniversalParser, UniversalParser>();

var app = builder.Build();

// TODO: Add application logic here

await app.RunAsync();
