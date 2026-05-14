# Sentinel

An AI-powered operations knowledge base for teams. Track incidents, manage runbooks, search documentation with semantic AI search, and get instant answers from an AI assistant trained on your own knowledge base.

---

## What It Does

**Incident Tracking** — Log and manage operational incidents with severity levels (Critical, High, Medium, Low), status tracking, and resolution timestamps.

**Runbook Management** — Store step-by-step operational procedures. Keep your team's playbooks organized and searchable.

**Knowledge Base** — Upload PDFs and Word documents or write entries manually. Every entry is automatically indexed with AI embeddings for semantic search — search by *meaning*, not just keywords.

**AI Chat Assistant** — Ask questions in plain English and get answers sourced directly from your knowledge base. The assistant cites which entries it used to answer.

**Dashboard** — Live stats showing open incidents, severity breakdowns, recent activity, and an alert banner for active Critical or High incidents.

---

## Tech Stack

- **Frontend / Backend** — Blazor Server (.NET 10)
- **Database** — PostgreSQL with pgvector (semantic search)
- **AI** — OpenAI (`text-embedding-3-small` for search, `gpt-4o-mini` for chat)
- **Auth** — ASP.NET Core Identity
- **Deployment** — Docker Compose (self-hosted) or Azure App Service

---

## Running Sentinel

### Option 1 — Self-Hosted with Docker (Recommended)

The easiest way to run your own Sentinel instance. Requires [Docker Desktop](https://www.docker.com/products/docker-desktop/) and an [OpenAI API key](https://platform.openai.com/api-keys).

**1. Copy the environment file:**
```bash
cp .env.example .env
```

**2. Fill in your values in `.env`:**
```
DB_PASSWORD=choose-a-strong-password
OPENAI_API_KEY=sk-your-openai-key-here
```

**3. Start Sentinel:**
```bash
docker-compose up -d
```

**4. Open your browser:**
```
http://localhost:8080
```

Register your account on first launch — the database is set up automatically.

To stop:
```bash
docker-compose down
```

---

### Option 2 — Azure (Cloud Hosted)

A live instance is deployed at:

```
https://sentinel.azurewebsites.net
```

Contact the administrator to request access.

---

## Updating to the Latest Version

```bash
docker-compose pull
docker-compose up -d
```

Your data is preserved — only the app updates.

---

## Requirements

| Requirement | Notes |
|-------------|-------|
| Docker Desktop | [Download here](https://www.docker.com/products/docker-desktop/) |
| OpenAI API key | [Get one here](https://platform.openai.com/api-keys) — pay-as-you-go, minimal cost for small teams |
| 2GB RAM | For Docker containers |

---

## Development Setup

```bash
# Clone the repo
git clone https://github.com/devbc10/sentinel.git
cd sentinel

# Set your local connection string and OpenAI key in appsettings.Development.json
# or use dotnet user-secrets

# Run locally
dotnet run --project src/Sentinel.Web/Sentinel.Web.csproj
```

Requires .NET 10 SDK and a local PostgreSQL instance with the pgvector extension.
