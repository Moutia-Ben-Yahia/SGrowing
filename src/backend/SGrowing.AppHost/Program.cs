var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure: PostgreSQL + Redis
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("sgrowing-postgres-data");

var db = postgres.AddDatabase("sgrowing");

var redis = builder.AddRedis("redis")
    .WithDataVolume("sgrowing-redis-data");

// SGrowing Api Module
var api = builder.AddProject<Projects.SGrowing_Api>("api")
    .WithReference(db)
    .WithReference(redis);

// SGrowing Worker Daemon Cluster
var worker = builder.AddProject<Projects.SGrowing_Worker>("worker")
    .WithReference(db)
    .WithReference(redis);

// SGrowing MCP Autonomous AI Server
var mcpserver = builder.AddProject<Projects.SGrowing_McpServer>("mcpserver");

builder.Build().Run();
