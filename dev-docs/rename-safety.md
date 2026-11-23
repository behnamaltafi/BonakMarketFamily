# BonakMarketFamily rename: deployment and data compatibility

This repository is a modified fork of [Smartstore](https://github.com/smartstore/Smartstore). The source projects, namespaces and application assemblies have been renamed to **BonakMarket**. This is **not** a guarantee that an existing Smartstore production installation can be replaced in-place without migration.

## Safe validation procedure

1. Take a full, verified **SQL Server backup** and a separate backup of `App_Data/Tenants`, including tenant configuration and installation markers. Do not publish over the only copy.
2. Use a staging IIS site and a **restored copy** of the original database. Do not let the test instance connect to production.
3. Copy the existing installation's tenant data and **reconcile** user settings with the new configuration root `BonakMarket` (`Smartstore` was the previous root). Do not blindly delete existing keys.
4. Compare module descriptors and persisted module registrations. In particular, the SMS plugin system identifier changed from `Smartstore.MultiSms` to `BonakMarket.MultiSms`; module assembly renames alone do not migrate persisted registrations or settings. Keep a rollback mapping and preserve provider/API credentials.
5. Verify database migration history and any assemblies/types persisted as strings before applying updates. Avoid re-running initial setup, dropping or recreating databases, or editing migration history without an explicit migration plan.
6. Validate application startup, account login, OTP SMS, SMS history, orders, themes, dynamic module loading, payment integrations, and PDF output on staging.
7. Publish a release artifact only after a successful **clean restore/build/test/publish**, staging upgrade, and rollback test. Preserve the original backup and artifact until production is stable.

## Build and package naming

- First-party project, assembly and namespace names use `BonakMarket`.
- Third-party NuGet package identities **must remain their published IDs**, even if they contain `Smartstore`: `Smartstore.Licensing`, `Smartstore.LightningCss.Native`, `Smartstore.EmbeddedSass.Net.Compiler`, and `Smartstore.wkhtmltopdf.Native.<RID>`.
- The .NET 10 `BonakMarket.ModuleBuilder` tool is built from source before module post-build; do not check in generated DLLs or runtime manifests.
- Docker image names are lowercase and use the repository owner's GHCR namespace: `ghcr.io/behnamaltafi/bonakmarket-linux` and `ghcr.io/behnamaltafi/bonakmarket-windows`.
- The GitHub Actions rename validation job checks static naming and performs a clean build and selected SMS tests, but it **does not** prove production database compatibility.

## Rollback

Stop the renamed application, restore the original application files including the original `App_Data/Tenants`, and restore the SQL Server backup if any write or migration occurred. Do not use a database changed by the renamed app with the old application until compatibility has been confirmed.

## Attribution

Retain upstream copyright and licensing notices and follow the existing repository AGPL obligations. Renaming source code does not transfer original authorship or ownership.