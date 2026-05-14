# DentaCare — Dental Clinic Management System

ASP.NET Core 9 MVC · Entity Framework Core · SQLite · Bootstrap 5

---

## Quick Start (Development)

```bash
dotnet run --project DentalClinic.Web
```

Default secretary login: `secretary@dentacare.com` / `Admin@123` (dev only)

---

## Production Configuration

The following values **must** be set via environment variables or a secrets manager before deploying. Never commit them to source control.

| Environment Variable | Description |
|---|---|
| `SeedSettings__DefaultSecretaryPassword` | Initial secretary account password |
| `ConnectionStrings__DefaultConnection` | Production DB connection string |
| `AllowedHosts` | Restrict to your domain, e.g. `dentacare.com;www.dentacare.com` |
| `SmtpSettings__Host` | SMTP server hostname |
| `SmtpSettings__Username` | SMTP username |
| `SmtpSettings__Password` | SMTP password |
| `SmtpSettings__FromAddress` | Sender email address |

Example (Linux/Docker):
```bash
export SeedSettings__DefaultSecretaryPassword="StrongPass@2026"
export AllowedHosts="dentacare.com;www.dentacare.com"
export SmtpSettings__Host="smtp.sendgrid.net"
```

---

## Running Tests

```bash
# All tests (unit + integration)
dotnet test DentalClinic.sln

# Unit tests only
dotnet test DentalClinic.Tests/

# Integration tests only
dotnet test DentalClinic.IntegrationTests/
```

---

## Project Structure

```
DentalClinic.Web/              # Main ASP.NET Core MVC application
  Controllers/                 # HTTP request handlers (role-segregated)
  Services/                    # Business logic + background services
  Models/                      # EF Core entity models
  ViewModels/                  # Form/display models with validation
  Views/                       # Razor views (Bootstrap 5)
  logs/                        # Serilog rolling log files (gitignored)

DentalClinic.Tests/            # xUnit unit tests (EF InMemory)
DentalClinic.IntegrationTests/ # xUnit integration tests (WebApplicationFactory)
```

---

## Health Check

```
GET /health
```

Returns `Healthy` / `Degraded` / `Unhealthy` based on database connectivity.
