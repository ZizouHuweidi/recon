var builder = DistributedApplication.CreateBuilder(args);


var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("recon-db");

var cache = builder.AddRedis("cache");

var goyim = builder.AddProject<Projects.recon_Goyim>("goyim")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.AddProject<Projects.recon_Mossad>("mossad")
    .WithHttpEndpoint(name: "http", targetPort: 5051)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc")
    .WithReference(cache)
    .WaitFor(cache);



builder.Build().Run();
