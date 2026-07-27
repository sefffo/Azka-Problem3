# Azka Maintenance Scheduling System - Problem 3

Enterprise Maintenance Scheduling System built with **.NET 8**, **Clean Architecture**, and **JWT Authentication**.

---

## Architecture

```
Azka.MaintenanceScheduling.sln
├── Azka.Domain                    → Entities, Enums, Repository Interfaces
├── Azka.Services                  → Service Interfaces, DTOs, FluentValidation
├── Azka.Services.Implementation   → Business Logic (AuthService, EngineerService, etc.)
├── Azka.Persistence               → DbContext, EF Configurations, GenericRepository, UnitOfWork
├── Azka.Presentation              → ASP.NET Core Controllers
├── Azka.Shared                    → ApiResponse<T>, PagedResult<T>
└── Azka.Web                       → Program.cs, Middleware, Configuration
```

## Technology Stack

- C# 12 / .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8 + SQL Server
- ASP.NET Core Identity
- JWT Bearer Authentication
- FluentValidation
- Swagger / Swashbuckle

---

## Getting Started

### Prerequisites
- .NET 8 SDK
- SQL Server (local or remote)
- Visual Studio 2022 / VS Code

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/sefffo/Azka-Problem3.git
   cd Azka-Problem3
   ```

2. **Update connection string** in `Azka.Web/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=AzkaMaintenanceSchedulingDb;Trusted_Connection=True;TrustServerCertificate=True;"
   }
   ```

3. **Apply migrations**
   ```bash
   cd Azka.Web
   dotnet ef migrations add InitialCreate --project ../Azka.Persistence
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run --project Azka.Web
   ```

5. **Access Swagger UI** at `http://localhost:5000`

---

## API Endpoints

### Auth
| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| POST | `/api/auth/register` | Register Admin or Dispatcher | No |
| POST | `/api/auth/login` | Login and get JWT token | No |

### Engineers
| Method | Endpoint | Description | Role |
|--------|----------|-------------|------|
| GET | `/api/engineers` | Get all active engineers | Any |
| GET | `/api/engineers/{id}` | Get engineer by ID | Any |
| GET | `/api/engineers/region/{region}` | Filter by region | Any |
| GET | `/api/engineers/{id}/workload?date=...` | Get workload for a date | Any |
| POST | `/api/engineers` | Create engineer | Admin |
| PUT | `/api/engineers/{id}` | Update engineer | Admin |
| DELETE | `/api/engineers/{id}` | Deactivate engineer | Admin |

### Assets
| Method | Endpoint | Description | Role |
|--------|----------|-------------|------|
| GET | `/api/assets` | Get all assets | Any |
| GET | `/api/assets/{id}` | Get asset by ID | Any |
| POST | `/api/assets` | Register new asset | Admin |
| DELETE | `/api/assets/{id}` | Delete asset | Admin |

### Work Orders
| Method | Endpoint | Description | Role |
|--------|----------|-------------|------|
| GET | `/api/workorders` | Get all work orders | Any |
| GET | `/api/workorders/{id}` | Get by ID | Any |
| GET | `/api/workorders/search` | Search/filter with pagination | Any |
| POST | `/api/workorders` | Create work order | Any |
| PATCH | `/api/workorders/{id}/status` | Update status | Any |
| DELETE | `/api/workorders/{id}` | Cancel work order | Any |

### Assignments
| Method | Endpoint | Description | Role |
|--------|----------|-------------|------|
| GET | `/api/assignments/engineer/{id}` | Get engineer's assignments | Any |
| GET | `/api/assignments/workorder/{id}` | Get work order's assignments | Any |
| POST | `/api/assignments` | Assign work order (conflict check) | Admin/Dispatcher |
| PUT | `/api/assignments/{id}/reschedule` | Reschedule (history preserved) | Admin/Dispatcher |
| PATCH | `/api/assignments/{id}/status` | Update status | Any |
| DELETE | `/api/assignments/{id}` | Cancel assignment | Admin/Dispatcher |

### Dashboard
| Method | Endpoint | Description | Role |
|--------|----------|-------------|------|
| GET | `/api/dashboard` | Operational dashboard summary | Any |

---

## Business Rules Implemented

1. ✅ Engineer cannot have overlapping assignments (conflict detection)
2. ✅ Every work order has exactly one active assignment
3. ✅ Emergency work orders flagged with high priority
4. ✅ Completed assignments cannot be modified
5. ✅ Assignment history preserved on reschedule/cancel
6. ✅ Engineers cannot exceed daily working capacity
7. ✅ Work order status synced automatically with assignment status
8. ✅ Cancelled work orders return to PendingAssignment queue
9. ✅ Every schedule modification is auditable via AssignmentHistory

---

## Database Migration Commands

```bash
# From solution root
dotnet ef migrations add InitialCreate --project Azka.Persistence --startup-project Azka.Web
dotnet ef database update --project Azka.Persistence --startup-project Azka.Web
```

---

## Roles

| Role | Permissions |
|------|-------------|
| **Admin** | Full access: create/update/delete engineers, assets, work orders, assignments |
| **Dispatcher** | Create and manage assignments and work orders |
| **Viewer** | Read-only access to all data |
