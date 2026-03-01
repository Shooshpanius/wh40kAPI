# wh40kAPI

A Warhammer 40,000 10th Edition API with a React frontend.

## Features

- **REST API** for all WH40K data: Factions, Datasheets, Abilities, Detachments, Stratagems, Enhancements, Sources
- **Swagger UI** via Scalar — three separate API docs:
  - WH40K API: `/scalar/wh40k`
  - BSData 40k: `/scalar/bsdata`
  - BSData Kill Team: `/scalar/ktbsdata`
- **Admin panel** (frontend at `/admin`) for uploading `Data.rar` to populate the database
- **MariaDB database** (schema auto-created on first run via EF Core `EnsureCreated`)

## Getting Started

### Requirements
- .NET 10 SDK
- Node.js 20+
- **MariaDB** (or MySQL-compatible) server

### Database Setup

Create the database and a dedicated user in MariaDB:

```sql
CREATE DATABASE wh40k CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'wh40k'@'localhost' IDENTIFIED BY 'changeme';
GRANT ALL PRIVILEGES ON wh40k.* TO 'wh40k'@'localhost';
FLUSH PRIVILEGES;
```

Edit the connection string in `appsettings.json` to match your setup, or **override it securely** without editing the file:

```sh
# .NET user secrets (development only)
cd wh40kAPI.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Port=3306;Database=wh40k;User=wh40k;Password=your_real_password;"

# Environment variable (any environment)
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=wh40k;User=wh40k;Password=your_real_password;"
```

### Run
```sh
cd wh40kAPI.Server
dotnet run
```

The app will be available at `https://localhost:51018` (Vite dev server).

## Admin Panel

The admin panel is protected by a password sent as the `X-Admin-Password` header.

The default password is **`admin123`**.

To change it, compute the SHA256 hash of your new password and update `AdminAuth:PasswordHash` in `appsettings.json`:

```sh
# Linux/macOS
echo -n "your_password" | sha256sum

# PowerShell
[System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes("your_password"))).ToLower()
```

## API Endpoints

### WH40K API (`/api/wh40k/`)

| Endpoint | Description |
|---|---|
| `GET /api/wh40k/factions` | All factions |
| `GET /api/wh40k/datasheets?factionId=SM` | Datasheets (filter by faction) |
| `GET /api/wh40k/datasheets/{id}/models` | Models for a datasheet |
| `GET /api/wh40k/datasheets/{id}/wargear` | Wargear for a datasheet |
| `GET /api/wh40k/abilities?factionId=SM` | Abilities |
| `GET /api/wh40k/detachments?factionId=SM` | Detachments |
| `GET /api/wh40k/strategems?factionId=SM` | Stratagems |
| `GET /api/wh40k/enhancements?factionId=SM` | Enhancements |
| `GET /api/wh40k/source` | Source books |
| `POST /api/wh40k/admin/upload` | Upload Data.rar (requires X-Admin-Password header) |
| `GET /api/wh40k/admin/status` | Database status (requires X-Admin-Password header) |

### BSData WH40K API (`/api/bsdata/`)

| Endpoint | Description |
|---|---|
| `GET /api/bsdata/catalogues` | All catalogues |
| `GET /api/bsdata/catalogues/{id}/units` | Units for a catalogue |
| `GET /api/bsdata/units` | All units (filter by `catalogueId`) |
| `GET /api/bsdata/units/{id}/profiles` | Profiles for a unit |
| `GET /api/bsdata/units/{id}/categories` | Categories for a unit |
| `GET /api/bsdata/units/{id}/infolinks` | Info links for a unit (rules, abilities, etc.) |
| `GET /api/bsdata/units/{id}/entrylinks` | Entry links for a unit (wargear, options, etc.) |
| `POST /api/bsdata/admin/import` | Import from BSData/wh40k-10e (requires X-Admin-Password header) |
| `GET /api/bsdata/admin/status` | BSData database status (requires X-Admin-Password header) |

### BSData Kill Team API (`/api/ktbsdata/`)

| Endpoint | Description |
|---|---|
| `GET /api/ktbsdata/catalogues` | All Kill Team catalogues |
| `GET /api/ktbsdata/catalogues/{id}/units` | Units for a catalogue |
| `GET /api/ktbsdata/units` | All units (filter by `catalogueId`) |
| `GET /api/ktbsdata/units/{id}/profiles` | Profiles for a unit |
| `POST /api/ktbsdata/admin/import` | Import from BSData/wh40k-killteam (requires X-Admin-Password header) |
| `GET /api/ktbsdata/admin/status` | Kill Team BSData database status (requires X-Admin-Password header) |

