var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var goyim = builder.AddProject<Projects.recon_Goyim>("goyim")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

builder.AddProject<Projects.recon_Mossad>("mossad")
    .WithHttpEndpoint(name: "http", targetPort: 5051)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

builder.AddProject<Projects.recon_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(goyim)
    .WaitFor(goyim)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

builder.Build().Run();
