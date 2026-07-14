<div align="center">

# ⚙️ AMOVER · LCM Backend API

### *The nervous system of the factory floor — from the first bolt to the final ride.*

A production, assembly, and after-sales management API for electric two-wheeled vehicle manufacturing.
Every order, every unit, every checklist, every service — one source of truth.

<br>

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![EF Core](https://img.shields.io/badge/EF_Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![JWT](https://img.shields.io/badge/Auth-JWT-000000?style=for-the-badge&logo=jsonwebtokens&logoColor=white)
![Swagger](https://img.shields.io/badge/Docs-Swagger-85EA2D?style=for-the-badge&logo=swagger&logoColor=black)

</div>

---

## 🌫️ Overview

**AMOVER LCM** is the backend that runs the shop floor. It turns a customer order into a
built, checked, and shipped vehicle — and keeps every step traceable in between.

The API sits between a **SQL Server** database (`LCMDatabase`) and the client applications
(a mobile companion app for operators, plus internal tooling). It exposes a clean,
token-secured REST surface that mirrors the real life-cycle of a unit on the assembly line:

> **Customer Order → Production Order → Unit Assembly → Quality Control → Packaging → Shipping → After-Sales**

Everything is designed around one idea: *a technician on the floor should get the full
picture of a unit in a single call, and update it just as easily.*

---

## ✨ Highlights

- 🔐 **JWT-secured by default** — every endpoint requires a token unless explicitly marked public. Login validates against ASP.NET Identity password hashes.
- 🏭 **Full production life-cycle** — create orders from customer orders, start them, run checklists, finalize, block, unblock, reopen, and mark as shipped.
- 📋 **Three-stage quality checklists** — Assembly, Packaging, and Control, generated automatically when an order starts.
- 🔧 **Serialized parts tracking** — register and validate serial numbers (VIN + component SNs) per unit against the model's parts template.
- 📊 **Live factory dashboard** — aggregated metrics computed in real time: units in production, blocked orders, pending controls, missing VINs, active crew.
- 🚨 **Calculated alerts** — critical and operational alerts derived on the fly from the current state of the database (no stale flags).
- 🛠️ **After-sales services** — maintenance, breakdowns, warranty, inspections, and technical campaigns, each with parts and status tracking.
- 📎 **One-call operational sheet** — `GET /api/ordens/{id}/ficha` returns the entire working file for a unit in a single request.

---

## 🧭 Domain Glossary

The codebase speaks Portuguese; here's the map for English readers.

| Term (code)        | Meaning                                                              |
|--------------------|---------------------------------------------------------------------|
| **Ordem**          | Production order — a unit being built on the line                    |
| **Mota**           | The physical vehicle / unit (identified by VIN)                      |
| **Modelo**         | Vehicle model (defines the parts template and required checklists)   |
| **Encomenda**      | Customer order — the commercial request an Ordem is created from     |
| **Cliente**        | Customer                                                             |
| **Checklist**      | Quality checklist (Assembly · Packaging · Control)                   |
| **Serviço**        | After-sales service (maintenance, breakdown, warranty…)             |
| **Peça**           | Part — either fixed (template) or serialized (tracked by SN)         |
| **Utilizador**     | User / operator assigned to a unit                                   |
| **Alerta**         | Real-time, calculated alert                                         |
| **Linha Montagem** | Assembly line                                                        |

---

## 🗺️ Life-cycle at a glance

```
   ┌──────────┐   iniciar    ┌──────────────┐   finalizar   ┌────────────┐
   │  ABERTA  │ ───────────▶ │  EM PRODUÇÃO │ ────────────▶ │ CONCLUÍDA  │
   │ (Open)   │              │ (In Progress)│               │ (Done)     │
   └──────────┘              └──────┬───────┘               └─────┬──────┘
        ▲                          │  bloquear                    │ reabrir
        │  desbloquear             ▼                              ▼
        └───────────────────  ┌──────────────┐            (back to production)
                              │  BLOQUEADA    │
                              │  (Blocked)    │
                              └──────────────┘
```

---

## 🧩 Tech Stack

| Layer            | Technology                                                        |
|------------------|-------------------------------------------------------------------|
| Runtime          | **.NET 8** (ASP.NET Core Web API)                                  |
| Language         | **C#** with nullable reference types + implicit usings            |
| Data access      | **Entity Framework Core 8** (SQL Server provider)                  |
| Database         | **Microsoft SQL Server**                                          |
| Auth             | **JWT Bearer** + **ASP.NET Identity** password hashing            |
| API docs         | **Swagger / Swashbuckle** with built-in `Authorize` button        |
| Serialization    | `System.Text.Json` (cycle-safe, null-omitting)                    |

---

## 📁 Project Structure

```
API_AMOVER/
├── API_AMOVER.sln
└── API_AMOVER/
    ├── Program.cs                 # App bootstrap: CORS, JWT, DbContext, Swagger
    ├── appsettings.json           # Connection string, JWT config, logging
    ├── Controllers/               # The REST surface (16 controllers)
    │   ├── AuthController.cs           # Login + identity
    │   ├── OrdensController.cs         # Production orders (the heart)
    │   ├── MotasController.cs          # Units, VINs, serialized parts
    │   ├── ModelosController.cs        # Models & parts templates
    │   ├── EncomendasController.cs     # Customer orders
    │   ├── ClientesController.cs       # Customers
    │   ├── ChecklistsController.cs     # Assembly / Packaging / Control
    │   ├── ServicosController.cs       # After-sales services
    │   ├── PecasController.cs          # Parts catalog
    │   ├── UtilizadoresController.cs   # Operators / crew
    │   ├── LinhaMontagemController.cs  # Assembly line operations
    │   ├── ControloFabricaController.cs# Factory control
    │   ├── DashboardController.cs      # Aggregated metrics
    │   ├── AlertasController.cs        # Calculated alerts
    │   ├── DocumentosController.cs     # Documents
    │   └── HealthController.cs         # Public health probe
    ├── Data/
    │   ├── LcmContext.cs          # EF Core DbContext
    │   └── Models/                # Entity models (domain + ASP.NET Identity)
    └── Migrations/                # EF Core migrations
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Access to a **SQL Server** instance hosting `LCMDatabase`
- (Optional) EF Core CLI: `dotnet tool install --global dotnet-ef`

### 1. Clone & restore

```bash
git clone <your-repo-url>
cd amover-lcm-backend-api/API_AMOVER/API_AMOVER
dotnet restore
```

### 2. Configure

Update `appsettings.json` with your own environment values — **never commit real
credentials or secrets.** Prefer user-secrets or environment variables in practice.

```jsonc
{
  "ConnectionStrings": {
    "LCMDatabase": "Server=<host>,1433;Database=LCMDatabase;User Id=<user>;Password=<secret>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Jwt": {
    "Key": "<a-long-random-secret-key>",
    "Issuer": "API_AMOVER",
    "Audience": "AMoverMobile",
    "ExpiresInMinutes": 120
  }
}
```

> ⚠️ **Security note:** the sample `appsettings.json` in this repo ships with a hard-coded
> connection string and JWT key for local development. Rotate these and move them to a
> secret store before anything leaves your machine.

### 3. Apply migrations (optional)

```bash
dotnet ef database update
```

### 4. Run

```bash
dotnet run
```

The API listens on `http://0.0.0.0:5137` (and `https://localhost:7285` on the HTTPS profile).
In **Development**, Swagger UI opens automatically at:

```
http://localhost:5137/swagger
```

---

## 🔐 Authentication

Every route is protected by a **fallback authorization policy** — a valid JWT is required
unless a route is explicitly `[AllowAnonymous]` (only `/api/auth/login` and `/api/health`).

**1. Log in:**

```http
POST /api/auth/login
Content-Type: application/json

{
  "usernameOrEmail": "operator@amover.example",
  "password": "••••••••"
}
```

**Response:**

```json
{
  "token": "eyJhbGciOiJI...",
  "userId": "…",
  "username": "operator",
  "email": "operator@amover.example",
  "roles": ["Operator"],
  "expiresInMinutes": 120,
  "idUtilizador": 42,
  "nomeUtilizador": "Ana Operator",
  "estadoUtilizador": 1,
  "estadoUtilizadorNome": "Ativo"
}
```

**2. Use the token** on every subsequent request:

```http
Authorization: Bearer eyJhbGciOiJI...
```

In Swagger, click **Authorize**, paste `Bearer {token}`, and you're in.

---

## 📡 API Reference

A guided tour of the most-used endpoints. Full, always-up-to-date docs live in **Swagger**.

### 🏭 Production Orders — `/api/ordens`

| Method | Route                                  | Description                                                            |
|--------|----------------------------------------|-----------------------------------------------------------------------|
| `GET`  | `/api/ordens?estado=X&includeNomes=true` | List orders; `includeNomes` enriches with customer & model names    |
| `GET`  | `/api/ordens/{id}/ficha`               | **Full operational sheet** — order + customer + model + unit + checklists + parts + crew + services in one call |
| `GET`  | `/api/ordens/{id}/resumo`              | Progress summary                                                      |
| `GET`  | `/api/ordens/{id}/historico`           | Calculated history (creation, transitions, VIN, completion)          |
| `GET`  | `/api/ordens/prontos-expedicao`        | Completed orders ready to ship (with unit, customer, model, VIN)     |
| `POST` | `/api/ordens/from-encomenda/{id}`      | Create a production order from a customer order                       |
| `POST` | `/api/ordens/{id}/iniciar`             | Start the order — generates checklists, validates the model          |
| `POST` | `/api/ordens/{id}/finalizar`           | Finalize — validates checklists, VIN, and serialized parts           |
| `POST` | `/api/ordens/{id}/bloquear`            | Block the order (reason required in body)                            |
| `POST` | `/api/ordens/{id}/desbloquear`         | Unblock (optional resolution note)                                   |
| `POST` | `/api/ordens/{id}/reabrir`             | Reopen a completed order back into production                        |
| `POST` | `/api/ordens/{id}/marcar-enviada`      | Mark shipped (transitions the unit to *Active*)                     |

### 🏍️ Units — `/api/motas`

| Method   | Route                              | Description                                             |
|----------|------------------------------------|--------------------------------------------------------|
| `GET`    | `/api/motas?estado=&ordemId=&semVin=` | List units with filters                             |
| `GET`    | `/api/motas/by-vin/{vin}`          | Look up a unit by VIN                                   |
| `GET`    | `/api/motas/{id}/pecas-sn/resumo`  | Serialized-parts summary (required vs. filled)         |
| `GET`    | `/api/motas/{id}/pecas-fixas`      | The model's fixed-parts template for this unit         |
| `POST`   | `/api/motas/{id}/pecas-sn`         | Register / update a part's serial number               |
| `PUT`    | `/api/motas/{id}`                  | Update color, mileage and VIN in one call              |
| `PUT`    | `/api/motas/{id}/estado`           | Update unit status                                     |

### 📊 Dashboard & Alerts

| Method | Route                    | Description                                                        |
|--------|--------------------------|-------------------------------------------------------------------|
| `GET`  | `/api/dashboard/resumo`  | Aggregated factory metrics + per-order status list                |
| `GET`  | `/api/alertas?tipo=&severidade=&ordemId=` | Real-time calculated alerts                     |

### 🔧 More resources

`/api/modelos` · `/api/encomendas` · `/api/clientes` · `/api/checklists` ·
`/api/servicos` · `/api/pecas` · `/api/utilizadores` · `/api/linhamontagem` ·
`/api/controlofabrica` · `/api/documentos` · `/api/health`

---

## 📚 State Reference

<table>
<tr><td valign="top">

**Order** *(`OrdemProducao.Estado`)*

| # | State        |
|---|--------------|
| 0 | Open         |
| 1 | In Production|
| 2 | Completed    |
| 3 | Blocked      |

</td><td valign="top">

**Unit** *(`Mota.Estado`)*

| # | State           |
|---|-----------------|
| 0 | In Production   |
| 1 | Active          |
| 2 | In Maintenance  |
| 3 | Discontinued    |

</td><td valign="top">

**Service** *(`Servico.Estado`)*

| # | State      |
|---|------------|
| 0 | Scheduled  |
| 1 | In Progress|
| 2 | Completed  |

</td></tr>
</table>

**Service types** — `1` Maintenance · `2` Breakdown · `3` Warranty · `4` Inspection ·
`5` Diagnosis · `6` Prep / Delivery · `7` Technical Campaign · `8` Other

**Checklist types** — `1` Assembly · `2` Packaging · `3` Control

---

## 🧠 Design Notes & Known Limitations

This backend was intentionally built to fit the **existing database schema** without
forcing migrations. A few behaviours follow from that:

- **Blocking reasons aren't persisted** — `/bloquear` accepts a reason and echoes it back, but there's no column to store it yet.
- **History is calculated, not logged** — `/historico` derives events from current state (creation dates, completion, unit registration). Transition timestamps are approximate; there is no audit table.
- **Shipping is a proxy** — with no dedicated `Shipping` table, `marcar-enviada` transitions the unit's status as a stand-in for a real dispatch record (carrier, waybill, ship date).
- **Alerts are stateless** — every alert is recomputed per request; nothing is stored.

Each of these is a clean extension point: an additive migration (audit table, shipping
table, block-reason column) would turn these proxies into fully persisted features.

---

## 🛣️ Roadmap Ideas

- [ ] `HistoricoOrdem` audit table for real state-transition logging
- [ ] `Expedicao` table: dispatch date, carrier, waybill, packaging date
- [ ] Persisted block reasons + previous-state restoration on unblock
- [ ] Quality alerts (incomplete checklists, missing serialized parts) computed in batch
- [ ] Secrets moved to environment / secret store; sample credentials removed

---

<div align="center">

**AMOVER · LCM** — *built on the line, for the line.*

Made with .NET 8 · Entity Framework Core · a lot of coffee ☕

</div>
