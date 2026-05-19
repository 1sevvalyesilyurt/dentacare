# DentaCare — Dental Clinic Management System

[![CI](https://github.com/1sevvalyesilyurt/dentacare/actions/workflows/ci.yml/badge.svg)](https://github.com/1sevvalyesilyurt/dentacare/actions/workflows/ci.yml)

ASP.NET Core 9 MVC · Entity Framework Core 9 · SQLite · Bootstrap 5

---

## Features

| Role | Capabilities |
|------|-------------|
| **Secretary** | Manage doctors (specialty, commission, working hours, leaves) · View all-doctor calendar · Create/cancel/reschedule appointments · Record payments · Generate PDF invoices · Manage services/treatments · View revenue dashboard |
| **Doctor** | Daily & weekly schedule · Patient notes · Earnings summary (commission-based) · Mark appointments as completed |
| **Customer** | Register & login · Browse available slots (real-time AJAX) · Book/cancel appointments · View history & payment records · Edit profile & password |
| **System** | 24-hour appointment reminders (background service, 60-min interval) · Slot conflict enforcement · Notification cleanup (30-day TTL) |

---

## Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Framework | ASP.NET Core MVC | 9.0 |
| ORM | Entity Framework Core | 9.0.4 |
| Database | SQLite | — |
| Auth | ASP.NET Core Identity | 9.0.4 |
| UI | Bootstrap 5 · Bootstrap Icons | 5.3 |
| Logging | Serilog (file + console) | 8.0.3 |
| PDF | QuestPDF (Community) | 2025.7.4 |
| Metrics | prometheus-net.AspNetCore | 8.2.1 |
| E2E Tests | Microsoft Playwright | 1.49.0 |

---

## Quick Start

### Development (dotnet run)

```bash
dotnet run --project DentalClinic.Web
# → http://localhost:5050
```

Default secretary login: `secretary@dentacare.com` / `Admin@123`

### Docker + Monitoring stack

```bash
# Start app + Prometheus + Grafana
SECRETARY_PASSWORD=StrongPass@2026! docker compose up -d

# http://localhost:8080  → DentaCare
# http://localhost:9090  → Prometheus
# http://localhost:3000  → Grafana (admin / admin)
```

In Grafana: **Dashboards → Import → ID `10915`** (ASP.NET Core & .NET Monitoring).

Metrics endpoint: `GET /metrics` — scraped automatically by Prometheus every 15 s.

---

## Running Tests

```bash
# Unit tests (55)
dotnet test DentalClinic.Tests/

# Integration tests (150)
dotnet test DentalClinic.IntegrationTests/

# Playwright E2E tests (58) — starts real server automatically
dotnet test DentalClinic.PlaywrightTests/

# Everything at once
dotnet test DentalClinic.sln
```

> **E2E prerequisites:** Chromium must be installed once.
> ```bash
> dotnet build DentalClinic.PlaywrightTests
> pwsh DentalClinic.PlaywrightTests/bin/Debug/net9.0/playwright.ps1 install chromium
> ```

---

## Architecture

Three-tier layered architecture with clear separation of concerns:

```
Browser (Bootstrap 5 + AJAX)
    ↕ HTTPS
Controllers  (role-scoped: Account · Appointment · Customer · Doctor · Secretary · Notification)
    ↕
Services     (BookingService · PaymentService · EmailService · IHostedService x2)
    ↕
Data         (AppDbContext — EF Core, SQLite, migrations, unique indexes)
    ↕
Identity     (ApplicationUser · Roles · Cookie auth · Lockout)
```

Unique DB constraint on `(DoctorId, AppointmentDate)` enforces slot conflict rules at the database level, in addition to application-layer validation.

---

## Project Structure

```
DentalClinic.Web/              # Main application
  Controllers/                 # One controller per role/feature area
  Services/                    # Business logic + 2 background services
  Models/                      # EF Core entities (ApplicationUser, Appointment, Payment…)
  ViewModels/                  # Validated request/response shapes
  Data/                        # AppDbContext + EF migrations
  Views/                       # Razor views (role-scoped folders)
  wwwroot/                     # Bootstrap 5, Icons, jQuery (local)
  logs/                        # Serilog rolling log files (gitignored)
  appsettings.Playwright.json  # Isolated test environment config

DentalClinic.Tests/            # xUnit unit tests — services & ViewModels (55)
DentalClinic.IntegrationTests/ # xUnit integration tests — HTTP, auth, security (150)
DentalClinic.PlaywrightTests/  # NUnit Playwright E2E — real browser, real DB (58)
  ServerFixture.cs             # Starts app on port 5052, fresh SQLite per run
  PlaywrightTestBase.cs        # Login helpers, BaseURL, auto tracing on failure
  Tests/                       # AuthTests · BookingTests · CustomerTests · DoctorTests
                               # SecretaryTests · NotificationTests

.github/workflows/ci.yml       # GitHub Actions CI pipeline

Dockerfile                     # Multi-stage production image (sdk:9.0 → aspnet:9.0)
.dockerignore                  # Excludes test projects, bins, secrets
docker-compose.yml             # App + Prometheus + Grafana stack
prometheus/prometheus.yml      # Scrape config (15s interval → /metrics)
grafana/provisioning/          # Auto-provisioned Prometheus datasource
```

---

## CI/CD Pipeline

GitHub Actions runs on every push and pull request:

```
Build (Release + vuln scan)
    ├── Unit & Integration Tests  (205 tests, xUnit)
    ├── Playwright E2E Tests      (58 tests, Chromium headless)
    └── Docker Build              (Dockerfile validation, BuildKit cache)
```

- NuGet packages, Playwright browsers, and Docker BuildKit cache are cached between runs.
- Test results published as PR check annotations (`dorny/test-reporter`).
- Playwright trace archives uploaded as artifacts on test failure.
- Build fails on known vulnerable NuGet packages (`dotnet list package --vulnerable`).

---

## Security Features

| Control | Detail |
|---------|--------|
| HTTPS + HSTS | Enforced in Production; redirects HTTP in all environments |
| Security headers | `X-Frame-Options: DENY` · `X-Content-Type-Options: nosniff` · `CSP` · `Referrer-Policy` · `Permissions-Policy` |
| Anti-forgery | `[ValidateAntiForgeryToken]` on every state-changing POST |
| Rate limiting | 10 login/min · 5 register/min (per IP, fixed-window) |
| Account lockout | 5 failed attempts → 15-minute lockout |
| Role authorization | `[Authorize(Roles = "...")]` on every controller class |
| Audit logging | Serilog `AUDIT` prefix on login success / failure / lockout |
| Health check | `GET /health` — EF Core DB connectivity probe |
| Metrics | `GET /metrics` — Prometheus scrape endpoint (HTTP request count, duration, in-flight) |

---

## Production Configuration

Set these via environment variables or a secrets manager. **Never commit them to source control.**

| Environment Variable | Description |
|---|---|
| `SeedSettings__DefaultSecretaryPassword` | Initial secretary account password |
| `ConnectionStrings__DefaultConnection` | Production DB connection string |
| `AllowedHosts` | Restrict to your domain, e.g. `dentacare.com;www.dentacare.com` |
| `SmtpSettings__Host` | SMTP server hostname |
| `SmtpSettings__Username` | SMTP username |
| `SmtpSettings__Password` | SMTP password |
| `SmtpSettings__FromAddress` | Sender email address |

Example (Linux / Docker):
```bash
export SeedSettings__DefaultSecretaryPassword="StrongPass@2026"
export AllowedHosts="dentacare.com;www.dentacare.com"
export ConnectionStrings__DefaultConnection="Data Source=/data/dentacare.db"
```

---

## Health Check

```
GET /health
```

Returns `Healthy` / `Degraded` / `Unhealthy` based on database connectivity.
