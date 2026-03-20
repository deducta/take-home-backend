# Document Intelligence Pipeline

**Time:** 2-3 hours | **Stack:** .NET 9, MassTransit, RabbitMQ, PostgreSQL

## Background

You've joined a team that processes documents through an AI enrichment pipeline. The system works in development with small payloads, but the team is struggling in production: documents get lost under load, the AI service gets overwhelmed.

Your job is to diagnose the issues and improve the system's resilience.

## Getting Started

```bash
docker compose up --build
```

- API: http://localhost:5100
- RabbitMQ Management: http://localhost:15672 (guest/guest)
- Postgres: localhost:5432 (postgres/postgres)

Submit a small batch to verify it works:
```bash
curl -X POST http://localhost:5100/api/documents/batch \
  -H "Content-Type: application/json" \
  -d '{"documents": [{"content": "Test invoice #1234", "type": "invoice"}]}'
```

Then run the load generator to see it break:
```bash
cd tests/LoadGenerator && dotnet run
```

## Your Tasks

### 1. Diagnose (30 min)

Explore the codebase and identify issues that would cause problems in production. Use the RabbitMQ management UI, logs, and the database to understand what's happening.

### 2. Fix & Improve (90 min)

Prioritize and fix the issues you found. You won't have time to fix everything perfectly — that's intentional. We want to see how you triage and what tradeoffs you make.

### 3. Write Up (30 min)

Add a `SOLUTION.md` explaining:
- What problems you found (and how you found them)
- What you fixed and why you chose that approach
- What tradeoffs you considered
- What you would do differently with more time

## Rules

- You MAY modify any service except `MockAiApi` (treat it as an external dependency you can't control)
- You MAY add NuGet packages
- You MAY restructure code however you see fit
- You SHOULD NOT spend time on unit tests (unless you find it helps you debug)
- You SHOULD prioritize impact over coverage — a few meaningful fixes beat many superficial ones
