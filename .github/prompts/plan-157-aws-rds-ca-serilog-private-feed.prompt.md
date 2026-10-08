---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [AWS Deployment] SCIM connector - RDS CA trust + SerilogInitializer 1.0.5 from private feed"
labels: "devtask"
assignees: ""
---

## Source and dependencies

Derived from **KloudIdentity – AWS Technical Implementation Plan** (Oct 7, 2026, Path A: Azure App Configuration,
Key Vault and Storage stay in Azure; compute on ECS Fargate; databases on Amazon RDS for SQL Server). Covers **WP2**
(RDS certificate trust in every .NET image) and the SCIM part of **WP3 part B** (console output for CloudWatch).
Source issue: [#157](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/157).

**Branch:** `feat/aws-deployment`, cut from **`origin/dev-2.0`** at `70c3452` (`v2.0.10-dev`). This repo's working
line is `dev-2.0` (165 commits ahead of `dev`); the PR targets `dev-2.0`.

**Depends on:** KloudIdentity-LogAggregation #34 — `KN.KI.LogAggregator.SerilogInitializer` 1.0.5, published to the
private GitHub Packages feed (1.0.0–1.0.4 are on nuget.org). Same pattern: AccessReviews #420, Mgt-Portal #548,
ITSM #36, Metaverse #318.

**Verification against the current codebase (`origin/dev-2.0` @ `70c3452`):**

- `deploy.yml` builds the root `dockerfile` (`-f ./dockerfile .`). `Microsoft.SCIM.WebHostSample/dockerfile` is not
  used by any workflow — left alone.
- Build stage: `COPY . .`, `WORKDIR /src/Microsoft.SCIM.WebHostSample`, `dotnet restore`, then `build`/`publish`
  with `--no-restore` → only the restore step needs feed credentials.
- Runtime stage (`aspnet:8.0-bookworm-slim`, root, no `USER`): `dev-2.0` installs `unixodbc`, `msodbcsql17` and
  `msodbcsql18` (UTS Archival / ODBC-integrated apps). `msodbcsql17` has no arm64 package → local image builds on
  Apple silicon need `--platform linux/amd64` (CI runs on amd64).
- SerilogInitializer 1.0.4 in `KN.KloudIdentity.Mapper.Domain.csproj` and `Microsoft.SCIM.WebHostSample.csproj`;
  `Serilog.Sinks.File` 6.0.0 direct in `Microsoft.SCIM.WebHostSample.csproj`.
- `Startup.cs:66` already calls `LoggingConfigurator.ConfigureLogging(_appSettings!.LoggingConfigs[0], "SCIMConnector")`
  — 1.0.5 is API-compatible.
- `SqlConnectionFactory.cs:17` and the Hangfire setup (`Startup.cs:202`) already honour `Database:AuthMode` —
  **no code change for RDS**; AWS sets `Database__AuthMode=Sql`.
- No `nuget.config`, no feed credentials in workflows. The org Actions secret `KLOUDYNET_NUGET_PASSWORD` (selected
  repositories) is visible to this repo.
- `build.yml` builds in `Microsoft.SCIM.WebHostSample/` without warnings-as-errors and tests
  `KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj`.

---

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**

On AWS the SCIM connector runs on ECS Fargate and talks to Amazon RDS for SQL Server. Two things block that:

1. **TLS to RDS fails.** Microsoft.Data.SqlClient validates the server certificate by default. RDS certificates
   are signed by the Amazon RDS CA, which the Debian runtime image does not trust.
2. **No logs in CloudWatch.** ECS ships container stdout to CloudWatch Logs, but SerilogInitializer ≤ 1.0.4 writes
   to the console only in DEBUG builds. 1.0.5 adds `EnableConsole`, set to `true` in the AWS App Configuration only.

1.0.5 is on the **private** feed, so the repo needs a `nuget.config` and feed credentials in CI and in the Docker
build. The image must keep working exactly as today on **Azure Container Apps**.

**Endpoint & Inputs:**

* **Route / Payload / Auth:** none — no API surface change (SCIM endpoints, `/scim/token` unchanged).
* **Build-time inputs:** `KLOUDYNET_NUGET_USERNAME` (`github.actor`), `KLOUDYNET_NUGET_PASSWORD` (org Actions secret).
* **Runtime config (AWS only):** App Configuration `KI:LoggingConfigs:0:EnableConsole = true`; env `Database__AuthMode=Sql`.

**Architectural Boundaries:**

* **Target Service:** `scimconnector-api` (root `dockerfile`).
* **Core Patterns:** OS trust store for TLS (never `TrustServerCertificate=True`); NuGet package source mapping (exact
  ID beats prefix); BuildKit secrets for private-feed restore — same as AccessReviews and Metaverse.
* **Infrastructure:** Amazon RDS for SQL Server (AWS) / Azure SQL (ACA); GitHub Packages NuGet feed
  `https://nuget.pkg.github.com/Kloudynet-Technologies/index.json`.

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

### Phase 1: RDS CA trust in the runtime image ✅ DONE

* **Logic:** root `dockerfile`, runtime stage, after `dev-2.0`'s OS-patch/ODBC `RUN`, before `COPY --from=build`:
  ```dockerfile
  ADD https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem /tmp/rds-global-bundle.pem
  RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
   && awk 'BEGIN{n=0} /BEGIN CERTIFICATE/{n++} {print > ("/usr/local/share/ca-certificates/rds-" n ".crt")}' /tmp/rds-global-bundle.pem \
   && update-ca-certificates && rm -rf /tmp/rds-global-bundle.pem /var/lib/apt/lists/*
  ```
  Own step so the ODBC block stays byte-for-byte unchanged; the OS store is also used by unixODBC/msodbcsql, so ODBC
  connections to RDS gain trust too. The parentheses around the awk output path are required (portability).
* **Agent Instruction:** "Only add CAs. Do not touch the ODBC block, base image, entrypoint or port."
* **Checkpoint:** `update-ca-certificates` logs `111 added`.

### Phase 2: SerilogInitializer 1.0.5 ✅ DONE

* **Logic:** `KN.KloudIdentity.Mapper.Domain.csproj`, `Microsoft.SCIM.WebHostSample.csproj`: 1.0.4 → 1.0.5;
  `Serilog.Sinks.File` 6.0.0 → 7.0.0 in `Microsoft.SCIM.WebHostSample.csproj` (1.0.5 depends on 7.0.0 → NU1605).
* **Agent Instruction:** "No code change — `ConfigureLogging` signature unchanged."
* **Checkpoint:** build green; all Mapper tests pass.

### Phase 3: Private feed wiring ✅ DONE

* **Logic:**
  * New root `nuget.config` (copy of AccessReviews'): `<clear/>`; sources nuget.org + `kloudynet`; `auditSources`
    nuget.org only; mapping nuget.org `*` + `KN.KI.LogAggregator.*`, kloudynet `KN.KI.*` + exact
    `KN.KI.LogAggregator.SerilogInitializer`; credentials `%KLOUDYNET_NUGET_USERNAME%` / `%KLOUDYNET_NUGET_PASSWORD%`.
  * `dockerfile`: `# syntax=docker/dockerfile:1.10` first line; `RUN --mount=type=secret,id=nuget_user,env=KLOUDYNET_NUGET_USERNAME,required=true --mount=type=secret,id=nuget_pass,env=KLOUDYNET_NUGET_PASSWORD,required=true dotnet restore`.
  * `build.yml`: job-level env `KLOUDYNET_NUGET_USERNAME: ${{ github.actor }}`,
    `KLOUDYNET_NUGET_PASSWORD: ${{ secrets.KLOUDYNET_NUGET_PASSWORD }}`.
  * `deploy.yml`: same env on the build step + `--secret id=nuget_user,env=KLOUDYNET_NUGET_USERNAME --secret id=nuget_pass,env=KLOUDYNET_NUGET_PASSWORD`;
    tag, `-f ./dockerfile`, context `.` unchanged.
* **Agent Instruction:** "Never pass feed credentials as `ARG` or `ENV`."
* **Checkpoint:** clean restore takes 1.0.5 from the GitHub feed and RabbitMq 0.0.6 from nuget.org.

### Phase 4: Release ⏳ TODO

* PR `feat/aws-deployment` → `dev-2.0`; CI green (the org secret is already visible to this repo); release a new
  `scimconnector-api` tag via `deploy.yml`.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

* **Coding Standards:** no application code changes; keep the Dockerfile's 🔐 comment style.
* **Security:** feed token only through BuildKit secrets (never in a layer, `docker history`, `ARG`, `ENV`); never
  `TrustServerCertificate=True` as a workaround.
* **Performance:** the CA layer sits before `COPY --from=build`, so it stays cached across code changes.
* **Prohibited:** no change to the ODBC/driver block, `Microsoft.SCIM.WebHostSample/dockerfile`, base images, ports,
  entrypoint, `Startup.cs` or SQL/Hangfire code. Base is `dev-2.0` — never merge `dev` into this branch.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:** a `scimconnector-api` image that trusts the Amazon RDS CAs and uses SerilogInitializer 1.0.5,
otherwise identical at runtime to the current `dev-2.0` image.

**Verification (done 2026-10-08 on the branch, base `dev-2.0` @ `70c3452`):**

* [x] `dotnet restore --no-cache --force` → SerilogInitializer 1.0.5 from `nuget.pkg.github.com/Kloudynet-Technologies`.
* [x] `dotnet build -c Release --no-restore` in `Microsoft.SCIM.WebHostSample/` (as `build.yml`) → 0 errors.
* [x] `dotnet test KN.KloudIdentity.MapperTests.csproj` → 554 passed, 0 failed.
* [x] Docker build with the `deploy.yml` command + BuildKit secrets (`--platform linux/amd64` locally) → `111 added, 0 removed`.
* [x] In-image: `deps.json` shows SerilogInitializer 1.0.5; ODBC Driver 17 and 18 registered; 111/111 RDS certificates
      trusted (X509Chain, OS store); no leftover temp files; token not in `docker history`.
* [x] In-image (ACA path): SqlClient to `sqlsvr-kloudidentity-demo.database.windows.net` with
      `Encrypt=True;TrustServerCertificate=False` → login error 18456 after a successful TLS handshake.
* [x] Env, Entrypoint, Cmd, ExposedPorts, WorkingDir, User identical to an image built from `origin/dev-2.0`.
* [ ] CI green on the PR into `dev-2.0`.
* [ ] Dev AWS: RDS connection with full certificate validation; logs in CloudWatch with `EnableConsole=true`.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

* **Impacted Components:** `dockerfile`, `nuget.config` (new), `.github/workflows/build.yml`,
  `.github/workflows/deploy.yml`, `KN.KloudIdentity.Mapper.Domain.csproj`, `Microsoft.SCIM.WebHostSample.csproj`.
* **Dependent Tasks:** LogAggregation #34 (1.0.5 published ✅); org secret `KLOUDYNET_NUGET_PASSWORD` visible to this
  repo ✅. KloudIdentity-CICD WP4 sets `KI:LoggingConfigs:0:EnableConsole = true` for AWS; WP8 creates the RDS
  database/login (`kidb-scim`, `ki_scim`).
* **Anti-Drift Log:**
  * First attempt branched from `dev` by mistake; the branch was deleted and recreated from `dev-2.0`.
  * One conflict when applying onto `dev-2.0`: the runtime stage gained an ODBC driver install — kept as is, RDS
    step placed after it.
  * The AWS plan listed SCIM as needing only the CA step; the private-feed move of SerilogInitializer (decided
    2026-10-08) added the `nuget.config` / CI / BuildKit-secret work.
