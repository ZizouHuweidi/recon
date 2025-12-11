# Recon API

.NET 10 Web API with **plug-and-play observability** - switch between Grafana, Signoz, or ClickStack without code changes.

## Quick Start

```bash
# 1. Start PostgreSQL
docker-compose up -d

# 2. Run API
dotnet run --project recon.Api
# API runs on http://localhost:5138

# 3. Test
curl http://localhost:5138/health
curl http://localhost:5138/users
```

---

## Observability Demo

**Switch backends by editing `recon.Api/appsettings.Development.json`:**

### Grafana / Signoz (No Auth)
```json
{
  "OTLP_ENDPOINT": "http://localhost:4317",
  "OTLP_HEADERS": ""
}
```

### ClickStack (Requires Auth)
```json
{
  "OTLP_ENDPOINT": "http://localhost:4317",
  "OTLP_HEADERS": "authorization=<your-ingestion-key>"
}
```

**Start ClickStack:**
```bash
cd observability/clickstack
docker compose up -d
# UI: http://localhost:8080
```

**Restart API after config change.**

---

## What's Exported

- ✅ **Traces** (OpenTelemetry SDK)
- ✅ **Metrics** (ASP.NET Core, HTTP, Runtime)
- ✅ **Logs** (Serilog → OpenTelemetry)

All via OTLP gRPC to port 4317.

---

## Services

| Service    | Port | URL                       |
|------------|------|---------------------------|
| API        | 5138 | http://localhost:5138     |
| PostgreSQL | 5432 | localhost:5432            |
| ClickStack | 8080 | http://localhost:8080     |
