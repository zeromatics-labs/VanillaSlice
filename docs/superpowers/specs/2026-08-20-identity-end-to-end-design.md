# VanillaSlice: Identity End-to-End (Phase A)

**Date:** 2026-08-20
**Status:** Approved (design) — implementation not started
**Scope:** Working ASP.NET Core Identity across all three generated hosts — Blazor Web (`WebPortal`), MAUI Hybrid (`HybridApp`), and MAUI Native (`MauiNativeApp`). Signup, signin, signout, email confirmation, password reset, and profile management.

**Programme context:** This is sub-project **A** of three. **B** = module system (manifest, selection, composition, `add` command, CI verification), extracted from what A teaches. **C** = module catalogue (User Management, Roles/Permissions, Realtime, Push, RAG, Files). Sequence is A → B → C. A is built hard-coded; the module system is extracted from it afterwards, not designed ahead of it.

---

## 1. Motivation

`README.md` reports Authentication as **✅ Complete** and JWT Token Support as **✅ Fully Implemented**. The templates do not support this claim. A developer who bootstraps with authentication enabled cannot register, cannot sign in, and cannot call an authorized endpoint on any of the three hosts.

This is not a missing feature. It is a **trust defect**: the wizard advertises a capability the output does not have, and the failure surfaces only after the developer has invested setup time. For an OSS generator whose adoption funnel is the first ten minutes, this is the single highest-severity issue in the repository.

### 1.1 Verified current state

| Location | Finding |
|---|---|
| `Templates/WebPortal/Components/Account/` | Only `Login.razor_`, `ExternalLoginPicker.razor_`, `StatusMessage.razor_` plus four support classes. No Register, ConfirmEmail, ForgotPassword, ResetPassword, or Manage pages. |
| `Templates/WebPortal/Program.cs:105` | `//app.MapAdditionalIdentityEndpoints();` — commented out. The login form has no endpoint to post to. |
| `Templates/WebPortal/Program.cs` | Pipeline has `UseAntiforgery()` but **never** `UseAuthentication()` or `UseAuthorization()`. |
| `Templates/WebPortal/Program.cs` | `RequireConfirmedAccount = true` paired with `IdentityNoOpEmailSender`. Even if registration existed, no account could ever be confirmed. Terminal dead-end. |
| `Templates/WebAPI/Program.cs` | No `AddAuthentication`, no `UseAuthentication`, no `UseAuthorization`, no identity endpoints. Nothing issues or validates a credential. |
| `Templates/HybridApp/Services/TokenHandler.cs` | `SendAsync` calls `base.SendAsync` unchanged. Injects `ILocalStorageService` and never reads it. Registered in `MauiProgram.cs` but never attached via `AddHttpMessageHandler<TokenHandler>()`. Entirely dead code. |
| `Templates/MauiNativeApp/` | No authentication files of any kind. |
| `Templates/HybridApp/MauiProgram.cs` | Base address hardcoded to `https://localhost:7202` for **both** DEBUG and RELEASE. Unreachable from an Android emulator, which requires `10.0.2.2`. |
| Both `Program.cs` files | Hardcode `options.UseSqlServer(connectionString)` regardless of the wizard's `DatabaseProvider` selection. |

### 1.2 What is already correct

The client-side abstraction needs no redesign. `Templates/ClientShared/Helpers/BaseHttpClient.cs` defines a `BaseHttpClient` interface with two implementations:

- `HttpTokenClient` — reads `auth_token` from `ILocalStorageService`, sets `Authorization: Bearer`, throws `UnauthorizedAccessException` on 401.
- `HttpCookieClient` — relies on ambient cookie credentials.

Slices consume the interface, so feature code is authentication-agnostic. Both MAUI hosts implement `ILocalStorageService` over `SecureStorage.Default` (platform Keychain / Android Keystore), so token storage is already secure.

---

## 2. Architecture

### 2.1 Decision record

| # | Decision | Chosen | Rejected alternatives |
|---|---|---|---|
| 1 | Sequencing | Identity hard-coded first, module system extracted from it | Module system designed first |
| 2 | Credential authority | `MapIdentityApi<ApplicationUser>()` | Hand-rolled JWT controller; external OIDC (Entra/Keycloak/Duende) |
| 3 | First-run email | `DevEmailSender` default **and** wizard provider option | Disabling `RequireConfirmedAccount`; requiring SMTP config at bootstrap |
| 4 | Screen authoring | Copy the `--auth Individual` scaffold for web; mirror an interactive set for MAUI | Single unified interactive set shared by all hosts |
| 5 | Flow scope | Tier 2 minimum (no path dead-ends); web reaches Tier 3 at no extra cost | Tier 1 (login only); Tier 3 authored by hand |

**Decision 4 was revised during design.** The initial recommendation was a single unified set of interactive Razor components serving all three hosts. Copying Microsoft's scaffold is better: it is the architecture Microsoft documents for this exact topology, it delivers 2FA and external logins at no authoring cost, and it removes the risk of hand-writing authentication flows.

### 2.2 Reference implementation

`dotnet/blazor-samples` → **`MauiBlazorWebIdentity`**, documented at *".NET MAUI Blazor Hybrid and Web App with ASP.NET Core Identity"*. It combines, on one server: the `--auth Individual` SSR Account pages, `app.MapGroup("/identity").MapIdentityApi<ApplicationUser>()`, and `MapAdditionalIdentityEndpoints()`. VanillaSlice splits these across two hosts (§2.3), but the per-host composition is the same.

### 2.3 Topology

```
                 AppDbContext — one Identity store, shared
                 ├───────────────────────┬───────────────────────┐
        WebPortal :7064                              WebAPI :7202
        AddIdentityCore + AddSignInManager           AddIdentityApiEndpoints
        AddAuthentication().AddIdentityCookies()
        SSR Account pages (SignInManager, in-process)
        MapAdditionalIdentityEndpoints()             MapGroup("/identity").MapIdentityApi()
        cookie scheme                                bearer scheme
                 │                                            │
        HttpCookieClient                              HttpTokenClient
                 │                                   ┌────────┴────────┐
        WebPortal + WebPortal.Client            HybridApp        MauiNativeApp
                                                (Razor over            (XAML over
                                                 shared VMs)            shared VMs)
```

Web authenticates **in-process** via `SignInManager`, so it never calls `/login?useCookies=true` and no cross-origin cookie problem arises. `MapIdentityApi` is therefore mapped on **WebAPI only**, serving bearer clients. Both hosts share `AppDbContext`, so there is one user store with two entry points.

### 2.4 Why `MapIdentityApi` (decision 2)

One call supplies the complete endpoint set: `POST /register`, `POST /login`, `POST /refresh`, `GET /confirmEmail`, `POST /resendConfirmationEmail`, `POST /forgotPassword`, `POST /resetPassword`, `POST /manage/2fa`, `GET|POST /manage/info`.

`POST /login` has two modes selected by query string: `?useCookies=true` establishes a cookie; omitted or `false` returns `{ tokenType, accessToken, expiresIn, refreshToken }`. `AddIdentityApiEndpoints` registers both `IdentityConstants.ApplicationScheme` (cookie) and `IdentityConstants.BearerScheme`.

We write no token issuance, no refresh rotation, and no revocation logic. For a code generator that hands this surface to every downstream user, not owning the credential cryptography is the correct posture.

**Constraint to document, not work around:** these tokens are **not JWTs**. They are opaque tokens proprietary to the ASP.NET Core Identity platform. Microsoft states the feature is "not intended to be a fully-featured identity service provider or token server." This is adequate for local-account authentication and is the documented path for native clients. Projects needing standards-based tokens want external OIDC, which is a phase-C module.

---

## 3. Server changes

### 3.1 `Templates/WebPortal/Program.cs`

WebPortal does **not** map `MapIdentityApi` (§2.3), so its service registration stays exactly as `dotnet new blazor --auth Individual` emits it: `AddIdentityCore<ApplicationUser>().AddSignInManager()`, with `AddAuthentication(…).AddIdentityCookies()` retained. This is consistent with decision 4 and keeps the change surface minimal — the existing registration block is already correct.

| Change | Detail |
|---|---|
| Keep | `AddIdentityCore` + `AddSignInManager` + `.AddIdentityCookies()` — unchanged |
| Add | `app.UseAuthentication()` and `app.UseAuthorization()`, placed **before** `app.UseAntiforgery()` |
| Uncomment | `app.MapAdditionalIdentityEndpoints();` |
| Replace | `IdentityNoOpEmailSender` → provider-selected sender (§5) |
| Fix | `UseSqlServer` hardcode → provider-conditional (§3.4) |

> **Conditional caveat, recorded so it is not rediscovered the hard way.** Microsoft documents that if a Blazor Web App template project moves to `AddIdentityApiEndpoints`, the generated `AddIdentityCookies` call "isn't necessary … and results in an error if left in the app," because `AddIdentityApiEndpoints` registers the cookie scheme itself. This specification avoids the situation by not mapping `MapIdentityApi` on WebPortal. **If a future change maps identity endpoints on WebPortal as well, `.AddIdentityCookies()` must be deleted in the same commit** or the host fails at startup.

### 3.2 `Templates/WebAPI/Program.cs`

Add `AddIdentityApiEndpoints<ApplicationUser>().AddEntityFrameworkStores<AppDbContext>()`, the email sender registration, `app.UseAuthentication()`, `app.UseAuthorization()`, and:

```csharp
app.MapGroup("/identity").MapIdentityApi<ApplicationUser>();
```

The `/identity` group prefix keeps identity routes from colliding with generated slice controllers and gives MAUI clients one stable base path.

### 3.3 `Templates/ServerData/EF/ApplicationUser.cs`

Unchanged for phase A. It already derives from `IdentityUser` and is the correct extension point. Profile fields belong to the phase-C User Management module.

### 3.4 Database provider fix (in scope)

Both `Program.cs` templates hardcode `UseSqlServer`. Identity is the first feature to ship EF migrations, so PostgreSQL and SQLite users hit this immediately and read it as "Identity is broken." Replaced with a `{{#if (eq DatabaseProvider …)}}` branch matching the wizard selection. Identity migrations are generated per provider.

This is adjacent scope, included deliberately: it is not possible to verify Identity on a non-SQL-Server provider without it.

### 3.5 Deletions

- `Templates/HybridApp/Services/TokenHandler.cs` — dead code, superseded by `HttpTokenClient`.
- Its `builder.Services.AddScoped<TokenHandler>()` registration in `Templates/HybridApp/MauiProgram.cs`.

---

## 4. Client changes

### 4.1 Web — copy the scaffold

Generate a throwaway project with `dotnet new blazor --auth Individual`, then copy `Components/Account/**` into `Templates/WebPortal/Components/Account/` wholesale, tokenizing namespaces to `{{ProjectName}}` and renaming to the `.razor_` convention.

This lands roughly twenty pages: Login, Register, RegisterConfirmation, ConfirmEmail, ConfirmEmailChange, ResendEmailConfirmation, ForgotPassword, ForgotPasswordConfirmation, ResetPassword, ResetPasswordConfirmation, Lockout, AccessDenied, InvalidUser, InvalidPasswordReset, ExternalLogin, LoginWith2fa, LoginWithRecoveryCode, Logout, plus `Manage/`: Index, Email, ChangePassword, SetPassword, TwoFactorAuthentication, EnableAuthenticator, ResetAuthenticator, GenerateRecoveryCodes, Disable2fa, ExternalLogins, PersonalData, DeletePersonalData.

Also copy `IdentityComponentsEndpointRouteBuilderExtensions.cs`, which supplies `MapAdditionalIdentityEndpoints()`.

Existing files are superseded by their scaffold equivalents. `IdentityNoOpEmailSender.cs` is replaced by §5.

**Consequence:** web reaches Tier 3 — including 2FA with TOTP/QR, recovery codes, and external login plumbing — for near-zero authoring cost, rather than deferring those to phase C.

### 4.2 MAUI — four screens, and why only four

MAUI Hybrid and Native each get: **Login, Register, Logout, ForgotPassword (request)**.

Confirm-email and reset-password are reached by **clicking a link in an email**, which opens a browser, not the app. Native screens for those flows would be unreachable — nothing can navigate to them. Those links land on WebPortal, which already serves them from §4.1. This is how the flows work on every platform; it is the correct design, not a reduction.

The reference sample ships Login and Logout only; this specification goes two beyond it.

### 4.3 Shared MAUI services

Mirroring the reference sample, added once and shared by both MAUI hosts:

| Class | Responsibility |
|---|---|
| `MauiAuthenticationStateProvider` | Extends `AuthenticationStateProvider`. `LogInAsync` / `LogOutAsync`; hydrates claims from `GET /manage/info`; raises `NotifyAuthenticationStateChanged`. |
| `TokenStorage` | Access token, refresh token, and expiry over the existing `SecureStorage`-backed `ILocalStorageService`. Refreshes near expiry to avoid re-login. |
| `HttpClientHelper` | Resolves the API base address per platform. **Required** — Android emulators cannot reach `localhost` and need `10.0.2.2`; iOS simulators differ again. Fixes the hardcode noted in §1.1. |

Registration in both `MauiProgram.cs` files:

```csharp
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<MauiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(s =>
    (MauiAuthenticationStateProvider)s.GetRequiredService<MauiAuthenticationStateProvider>());
```

### 4.4 Token refresh

`HttpTokenClient` currently has no 401 → `POST /refresh` → retry path. All four methods duplicate the same header-attachment block, so refresh handling is added once in a shared private helper rather than four times. `TokenStorage` owns expiry-driven proactive refresh; `HttpTokenClient` owns reactive 401 recovery.

### 4.5 Route gating

- Shared components requiring auth get `@attribute [Authorize]`.
- Each host's `Routes.razor` uses `AuthorizeRouteView` with a `<NotAuthorized>` fallback to Login.
- Nav menus wrap entries in `AuthorizeView`.
- `MauiNativeApp` gates `AppShellTabs` / `AppShellFlyout` routes on authentication state.

### 4.6 `ILocalStorageService` hardening

Both MAUI implementations declare `GetValue([CallerMemberName] string memberName = "")`. A caller that omits the key silently stores under the **calling method's name**. Explicit callers are unaffected, but identity code stores several related keys and is exactly where this misfires. Keys are passed explicitly everywhere, and the identity keys are centralised as constants on `TokenStorage`.

Separately, `Templates/FrameworkCore/Interfaces/ILocalStorageService.cs` declares `Task<string?> GetValue(…)` while `Services/FrameworkCoreGenerator.cs:218` declares `Task<string>` — two divergent definitions of one interface across the template-based and code-generated paths. Noted; reconciled only if it obstructs implementation.

---

## 5. Email

`RequireConfirmedAccount` stays `true`. Registration must exercise the real confirmation flow.

**`DevEmailSender`** (Development default) implements `IEmailSender<ApplicationUser>` and writes each message to `ILogger` **and** to `sent-emails/*.html` under the content root. The file output is not redundant: a developer testing on a phone or emulator has no console to read a confirmation link from.

**Wizard option** `EmailProvider: Dev | SMTP | SendGrid`, defaulting to `Dev`. The production path is a dropdown, not a rewrite. `SMTP` and `SendGrid` generate a configured `IEmailSender<ApplicationUser>` reading credentials from configuration; neither is required for first run.

---

## 6. Wizard and configuration

`Models/ProjectConfiguration.cs` gains:

| Member | Type | Default |
|---|---|---|
| `EmailProvider` | `EmailProvider` enum (`Dev`, `Smtp`, `SendGrid`) | `Dev` |
| `EmailFromAddress` | `string` | `noreply@localhost` |

`IncludeAuthentication` remains the master switch and continues to gate all of the above. `ProjectWizard.razor` gains the email provider control within the existing authentication section.

---

## 7. Verification

Given that this specification exists because a status matrix asserted a capability the output lacked, verification is a deliverable, not a follow-up.

**Smoke test** — generate a project, apply migrations, then drive:

```
register → read confirmation link from sent-emails/ → confirm
  → login → call an [Authorize] endpoint → refresh token → logout
  → confirm the authorized endpoint now returns 401
```

**Coverage:** WebPortal and HybridApp in CI. MauiNativeApp manually for phase A, with a tracked item to automate. Run across each `DatabaseProvider` (§3.4 makes this meaningful) and, at minimum, the Bootstrap and Tailwind UI frameworks.

**README correction** — required, and part of this work:

- Authentication remains ✅ only once the smoke test passes.
- **"JWT Token Support — ✅ Fully Implemented" becomes "Bearer token authentication (ASP.NET Core Identity tokens, not JWT)."** The current wording is inaccurate under decision 2 and would remain inaccurate after implementation.

---

## 8. Known limitations

1. **The scaffold is Bootstrap-only.** Copied Account pages carry Bootstrap markup with no `{{#if (eq UIFramework …)}}` branches, unlike `ProductForm.razor_`. In Tailwind, Fluent, MudBlazor, or Radzen projects they will look off-brand. **Accepted for phase A** — working beats matching. Per-framework re-skinning is a tracked follow-up; Tailwind ranks first, given it is the direction of current branding work.

2. **Feature asymmetry between web and MAUI.** Web gets 2FA, external logins, and personal-data management; MAUI gets four flows. This follows from §4.2 and from the scaffold's generosity. It is a genuine gap only for 2FA login on mobile, which is tracked separately.

3. **Identity tokens are not JWTs** (§2.4). Projects needing standards-based tokens or SSO want the external-OIDC module in phase C.

4. **MAUI Native is verified manually** in phase A (§7).

---

## 9. Feeding phase B

Phase A is deliberately built hard-coded, but the module system is extracted from it, so the following are recorded as they are encountered rather than reconstructed later:

- every file added or modified, by target project
- every DI registration, and its required ordering
- every `Program.cs` pipeline insertion, and its position constraints
- migrations added, and their provider variants
- wizard options introduced, and what they gate
- navigation and route-gating edits

The recurring shape — *copy a file set, tokenize, register DI, insert pipeline steps, add a migration, add nav entries* — is the module manifest's requirement list. Identity is module #1; it should be re-expressible through the phase-B manifest without changing its generated output.
