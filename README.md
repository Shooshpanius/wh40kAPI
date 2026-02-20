# wh40kAPI

A Warhammer 40,000 10th Edition API with a React frontend.

## Features

- **REST API** for all WH40K data: Factions, Datasheets, Abilities, Detachments, Stratagems, Enhancements, Sources
- **Swagger UI** via Scalar at `/scalar/v1` (development mode)
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

| Endpoint | Description |
|---|---|
| `GET /api/factions` | All factions |
| `GET /api/datasheets?factionId=SM` | Datasheets (filter by faction) |
| `GET /api/datasheets/{id}/models` | Models for a datasheet |
| `GET /api/datasheets/{id}/wargear` | Wargear for a datasheet |
| `GET /api/abilities?factionId=SM` | Abilities |
| `GET /api/detachments?factionId=SM` | Detachments |
| `GET /api/strategems?factionId=SM` | Stratagems |
| `GET /api/enhancements?factionId=SM` | Enhancements |
| `GET /api/source` | Source books |
| `POST /api/admin/upload` | Upload Data.rar (requires X-Admin-Password header) |
| `GET /api/admin/status` | Database status (requires X-Admin-Password header) |

