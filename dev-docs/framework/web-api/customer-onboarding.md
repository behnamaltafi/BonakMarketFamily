# Customer onboarding and address management

Identity v1 uses the original Customer entity, with the authenticated CustomerId derived from the session. Do not supply a customer id, username, or phone in profile updates.

## Deployment prerequisites
- Back up the database and apply the native migrations, including CustomerGuild.
- Ensure the Identity module is installed, Iran (ISO IR) is published and enabled for billing/shipping in each target store, and its provinces are configured.
- Create at least one active guild in Admin > Customers > Guilds before requiring onboarding. No example guilds are seeded.
- Configure HTTPS, SMS providers and the existing MobileApi JWT settings for mobile clients.
- Existing verified phone accounts synchronize the username at the next OTP login/profile save; conflicting usernames are rejected, not reassigned. Old bound cookie sessions must sign in again after upgrade.

## Profile
GET /api/identity/v1/guilds returns active guild id/title options.
GET and PUT /api/identity/v1/me/profile use the authenticated customer. PUT accepts firstName, lastName, email, nationalCode and guildId only. Names, email and national code are optional; an active guild is required for ordinary registered customers. National code is stored as a string and is not unique. An absent email for an SMS account uses the internal reserved accounts.invalid address, which is not a deliverable mailbox.

Login/session and profile responses expose profileRequired and missingRequiredFields. A missing or inactive guild blocks checkout, not catalog browsing or cart editing. The server checks again before placing a new order. Administrative/system accounts are exempt from the customer onboarding policy; existing recurring payments are not invalidated.

## Addresses
Under /api/identity/v1/me/addresses:
- GET ?page=1&pageSize=50: paged list (maximum page size 100, no total address limit).
- GET settings: fixed Iran, provinces, field visibility/required flags and salutations.
- GET {id}: owned address.
- POST: create (201).
- PUT {id}: update (200).
- PUT {id}/default: choose default (200).
- DELETE {id}: unlink from customer (204), preserving historical orders and shared records.

Use the settings response to render firstName, lastName, phoneNumber, stateProvinceId, city, address1, address2, zipPostalCode, company, salutation, title, faxNumber and isDefault. Country and email are server-assigned. The first valid address becomes default; deletion selects the oldest remaining valid candidate or clears the default. A shared/historical address cannot be edited in place (409); create a replacement instead. Non-owned ids return 404.

## Change verified phone
Under /api/identity/v1/me/security/phone:
1. POST reauth/request: send an OTP to the current verified number.
2. POST reauth/verify: verify the old number's code and obtain a short-lived reauthToken (10 minutes).
3. POST request: request the new number's OTP with phone and reauthToken.
4. POST verify: submit phone, code and reauthToken. The proof and code are consumed once; username changes with the verified number, old mobile sessions are revoked, and the customer must log in again.
For initial enrollment of an unbound authenticated account, the old-phone proof is not required.

Use the Identity Swagger document for actual request/response DTOs. The customer API requires Bearer access tokens over HTTPS; MVC address and phone widgets use separate same-origin cookie + antiforgery endpoints.

## Verification
Build BonakMarket.sln and run CustomerOnboardingContractTests, SmsApiControllerTests, StorefrontContractTests and SwaggerContractTests. Test the MVC onboarding page, admin guild creation/deactivation, first/default/removed addresses, cross-customer access, checkout with and without active guild, and phone change with old-cookie/refresh-token replay on a staging database before production deployment.
