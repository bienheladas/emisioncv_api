# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build
dotnet build

# Run (development)
dotnet run --project Minedu.VC.Issuer.csproj

# Restore dependencies
dotnet restore
```

There are no automated tests in this solution.

## Application Purpose

**Minedu.VC.Issuer** is a Verifiable Credential (VC) issuer API for Peru's Ministry of Education. It issues W3C Verifiable Credentials representing student academic records (certificates of studies), anchors them to the Cardano blockchain for immutability, and implements the OpenID Connect for Verifiable Credentials (OID4VCI) protocol for wallet-compatible issuance.

## Architecture

Layered architecture: Controllers → Services → Repositories → EF Core (SQL Server).

**Request flow for credential issuance:**
1. Client calls `POST /offer` → creates pre-authorized code linked to a `solicitudId`
2. Wallet calls `GET /offer/{code}` → receives OID4VCI credential offer metadata
3. Wallet calls `POST /token` with pre-authorized code → receives opaque Bearer token (HMAC-signed)
4. Wallet calls `POST /issuer/credential` with Bearer token → triggers full issuance pipeline:
   - Fetches student academic data from SQL Server (`RequestService`)
   - Maps DB entities to `CredentialSubject` (`CredentialSubjectMapper`)
   - Builds `VerifiableCredential` with status list entry (`VCBuilder`)
   - Signs with Ed25519 JWS2020 detached payload (`SignatureService`)
   - Anchors VC hash to Cardano blockchain (`CardanoAnchorService`)
   - Persists credential to `CERT_CREDENCIAL_VERIFICABLE`

**Revocation flow:**
- `POST /status/revoke/{index}` flips a bit in the `BitstringStatusList`
- Status list is re-serialized (gzip + base64url), re-signed, re-anchored to Cardano
- Status list credential served at `GET /status/1`

## Key Components

| Component | Location | Purpose |
|---|---|---|
| `VCBuilder` | `Services/` | Constructs W3C VC with `credentialStatus` entry |
| `SignatureService` | `Services/` | Ed25519 JWS2020 with detached payload; reads key from `Keys/issuer-key.json` |
| `CardanoAnchorService` | `Services/Cardano/` | Orchestrates blockchain anchoring |
| `CardanoTxGenerator` | `Services/Cardano/` | Builds/signs Cardano transactions (CardanoSharp) |
| `CardanoTxSubmitter` | `Services/Cardano/` | Submits via Blockfrost API (custom DLLs in `/libs/blockfrost/`) |
| `StatusListService` | `Services/` | Manages bitstring allocation, revocation, serialization |
| `AuthorizationService` | `Services/Auth/` | Pre-authorized code flow; HMAC opaque token generation/validation |
| `RequestService` | `Services/` | Aggregates student data from DB (solicitud + estudiante + grado + notas) |
| `CredentialSubjectMapper` | `Services/Mapper/` | Maps DB entities → `CredentialSubject` model |

## Data Model

SQL Server tables accessed via EF Core (stored procedure / raw SQL queries):
- `CERT_SOLICITUD` — credential request (links student to issuance)
- `CERT_ESTUDIANTE` — student identity data
- `CERT_GRADO` — academic degree/level info
- `CERT_NOTAS` — grade records
- `CERT_OBSERVACIONES` — observations/annotations
- `CERT_CREDENCIAL_VERIFICABLE` — issued VCs (persisted JSON + Cardano tx hash)

## Configuration (`appsettings.json`)

Key sections:
- **ConnectionStrings** — SQL Server connection to `db_certificado_calidad`
- **Issuer** — DID (`did:web:sistemas02.minedu.gob.pe`), base URL, credential config IDs
- **Blockfrost** — API key and network (`preprod`)
- **Cardano** — mnemonic, sender/receiver addresses for anchoring transactions
- **OIDC4VCI** — token TTL, pre-authorized code settings
- **Serilog** — structured logging with file output path

The private key for signing is stored in `Keys/issuer-key.json` (JWK format, Ed25519).

## Standards & Protocols

- **W3C Verifiable Credentials Data Model** — VC structure and JSON-LD contexts
- **OID4VCI (OpenID for Verifiable Credential Issuance)** — credential offer + pre-authorized code flow
- **JWS2020 (JsonWebSignature2020)** — Ed25519 signature with detached payload
- **BitstringStatusList** — W3C revocation mechanism (gzip-compressed bitstring, base64url-encoded)
- **Cardano metadata anchoring** — VC hash stored in Cardano transaction metadata (preprod network)

## Dependencies of Note

- `CardanoSharp.Wallet` — Cardano transaction building and signing
- `NSec.Cryptography` — Ed25519 key operations
- `SimpleBase` — Multibase/Base58 encoding
- `Polly` — Retry policies for Blockfrost API calls
- `Blockfrost.Api` — Custom wrapper DLLs (not from NuGet; located in `/libs/blockfrost/`)

## Stack
- .NET Core 9 / C#
- PostgreSQL (migrado desde SQL Server)
- ORM: [Entity Framework Core / Dapper — especifica el tuyo]

## Contexto de migración
- Origen: SQL Server
- Destino: PostgreSQL
- Prioridad actual: adaptar DbContext, connection strings y queries específicos de T-SQL
