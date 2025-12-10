var builder = DistributedApplication.CreateBuilder(args);


var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("recon-db");

var goyim = builder.AddProject<Projects.recon_Goyim>("goyim")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc")
    .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", "x-hyperdx-api-key=9c1f90dd-227a-4c86-a832-f7ed3b833bdf")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.AddProject<Projects.recon_Mossad>("mossad")
    .WithHttpEndpoint(name: "http", targetPort: 5051)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc")
    .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", "x-hyperdx-api-key=9c1f90dd-227a-4c86-a832-f7ed3b833bdf");



builder.Build().Run();
