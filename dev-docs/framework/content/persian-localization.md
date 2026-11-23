# Persian (Iran) storefront localization

This branch adds an installable `fa-IR` culture for BonakMarket's current seed format.

## New installations

Choose **فارسی (ایران)** as the installation language. The `fa-IR` resource catalog, Persian installer labels, RTL text direction and initial core topic translations are loaded by BonakMarket's seed mechanism. The existing Flex theme already provides an RTL stylesheet; its Persian-specific font stack has been enabled without bundling proprietary font files.

## Existing installations

Installation seeds are not automatically re-applied to an existing database. Open **Configuration > Regional Settings > Languages**, add/enable **Persian (Iran)** with culture `fa-IR`, SEO code `fa`, RTL enabled, and flag `ir.png`, and import `src/BonakMarket.Web/App_Data/Seed/cultures/fa-IR/resources/all.smres.xml` as that language's XML resources. Publish/assign the language to the store and make it the preferred/default language in the admin language/store configuration. Restart or clear localization caches if required.

Store-specific content (products, categories, menus, CMS pages, and previously installed sample data) is stored in the database and must be translated using the localized entity editor; the seed overlays only apply on new installations. Do not rerun the installer against an existing database.

## Translation coverage and attribution

The base Persian translations are adapted from [pineportal/PersianSmartstore, 6.4.0.x](https://github.com/pineportal/PersianSmartstore), licensed under AGPL-3.0, matching the parent project. They have been reconciled by resource key with the English resources in this checkout. Text entries missing from the source translation or containing incompatible formatting placeholders fall back to the corresponding English value. SMS/OTP plugin resources remain in `src/BonakMarket.Modules/BonakMarket.MultiSms/Localization/resources.fa-ir.xml` and are unchanged.

For complete localized operation, translate the remaining English fallback keys, all installed module resources, and business content, then test forms, checkout, account and administration screens in RTL.

## Validation

Run `dotnet build BonakMarket.sln -c Release`, then seed tests and UI smoke tests against a disposable SQL Server database. Verify `<html lang="fa" dir="rtl">`, checkout, date/number formatting, OTP messages, and proper isolation of phone/code inputs.
