# Recon Telemetry Test Server

A minimal Go HTTP server to test telemetry stacks (Grafana & ClickStack).

## Features
- OpenTelemetry traces, metrics, and logs
- Standard Go 1.23 HTTP router with method-based routing
- Configurable OTLP endpoint and API key headers
- Works with both Grafana Alloy and HyperDX/ClickStack

## Quick Start

### 1. Run the Go server

```bash
# Default: connects to localhost:4317
go run cmd/main.go

# With custom OTLP endpoint and API key
OTLP_ENDPOINT=your-otel-collector:4317 \
OTLP_HEADERS=x-hyperdx-api-key=your-api-key \
PORT=7777 \
go run cmd/main.go
```

### 2. Run telemetry stacks

**For Grafana:**
```bash
cd observability/grafana
docker compose up -d
# Access Grafana at http://localhost:3000
```

**For ClickStack/HyperDX:**
```bash
cd observability/clickstack
# Set required environment variables in .env file, then:
docker compose up -d
```

### 3. Generate traffic

```bash
curl http://localhost:7777/health
curl http://localhost:7777/api/test
curl http://localhost:7777/users
curl http://localhost:7777/api/error
```

## API Endpoints

- `GET /` - Root endpoint
- `GET /health` - Health check
- `GET /users` - Get users list
- `POST /users` - Create user
- `GET /api/test` - Test endpoint with metrics
- `GET /api/error` - Error endpoint for testing

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `OTLP_ENDPOINT` | `localhost:4317` | OTLP collector endpoint |
| `OTLP_HEADERS` | - | API key headers (format: `key=value`) |
| `PORT` | `7777` | Server port |

## For ClickStack/HyperDX

Set the API key header:
```bash
OTLP_HEADERS=x-hyperdx-api-key=your-api-key-here
```

## Build Binary

```bash
go build -o bin/recon-go ./cmd/main.go
./bin/recon-go
```
