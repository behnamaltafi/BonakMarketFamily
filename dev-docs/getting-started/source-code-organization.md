---
description: Structure of the BonakMarket repository and solution
---

# Source code organization

BonakMarket's repository keeps concerns separated and supports modular development.

## Solutions

* `BonakMarket.sln` – main solution containing application projects, modules, and tests.
* `BonakMarket.Tools.sln` – separate solution for tooling and utilities.

## Top-level directories

| Path     | Purpose                                                                                           |
| -------- | ------------------------------------------------------------------------------------------------- |
| `src/`   | Application source code: core libraries, domain layer, web host, database providers, and modules. |
| `test/`  | Unit and integration tests mirroring the structure of `src/`.                                     |
| `build/` | Build automation scripts and configuration.                                                       |
| `tools/` | Additional tools used during development and build.                                               |

## `src/` breakdown

* **BonakMarket** – cross-cutting infrastructure (caching, I/O, routing, engine runtime).
* **BonakMarket.Core** – domain models, business logic, migrations, and service abstractions.
* **BonakMarket.Web** – ASP.NET Core host with controllers, views, and configuration.
* **BonakMarket.Web.Common** – shared web helpers, MVC components, theming, and bootstrapping.
* **BonakMarket.Modules** – optional features like payment or shipping providers. Each module is a separate project with its own resources and optional migrations.
* **BonakMarket.Data.**\* – database provider packages (SQL Server, MySQL, PostgreSQL, SQLite) keeping data access provider-specific.

During a build, modules from `src/BonakMarket.Modules` are compiled and copied to `src/BonakMarket.Web/Modules` for runtime loading.

## Tests

Test projects reside in the `test/` directory and follow the layout of `src/`. Typical projects include `BonakMarket.Tests`, `BonakMarket.Core.Tests`, and module-specific tests. All tests run via:

```bash
dotnet test BonakMarket.sln
```
