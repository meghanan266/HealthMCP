# HealthMCP

A healthcare-domain Model Context Protocol (MCP) server built in .NET 9 that exposes clinical tools to any MCP-compatible AI client including Claude Desktop, Cursor, and Semantic Kernel agents. Implements both HTTP/SSE and stdio transports.

## Overview

AI assistants have no standardized way to reach into clinical systems and pull structured data. MCP defines a single protocol for tool discovery and invocation so clients do not need bespoke integrations. HealthMCP is a healthcare-oriented MCP server: any MCP-compatible client connects once and gains access to patient summaries, FHIR-shaped resources, clinical decision-support checks, and a queryable SQLite clinical database without knowing how those capabilities are implemented.

## Tools (12 total)

### PatientTools

- **get_patient** — Returns full demographic and clinical details for a patient by ID.
- **list_patients** — Lists all patients with ID, name, and active conditions.
- **get_patient_medications** — Returns current medications for a patient, with the patient name in the header.

### ClinicalQueryTools

- **query_clinical_data** — Runs a read-only SQL SELECT against the clinical SQLite database; results are pipe-formatted (max 50 rows).
- **list_clinical_tables** — Lists user-defined tables in the clinical database with their full CREATE TABLE statements.

### FHIRTools

- **get_fhir_resource** — Returns a stored FHIR R4 JSON resource for a patient and resource type.
- **validate_fhir_resource** — Validates a FHIR JSON document for basic structure (resourceType, id, supported type).
- **extract_clinical_codes** — Walks a FHIR JSON document and lists all objects that include system and code (typical codings).

### ClinicalAlertTools

- **check_drug_interactions** — Checks a static reference list for clinically significant drug–drug interactions.
- **flag_abnormal_vitals** — Flags abnormal vital signs using predefined WARNING and CRITICAL thresholds.
- **evaluate_readmission_risk** — Estimates readmission-related risk from recent completed visits and diagnosis count in the clinical database.

## Architecture

- **HealthMCP.Server** — ASP.NET Core web app: HTTP/SSE transport, MCP endpoint at `/mcp`, health check at `/health`, listens on port 5100 (via configuration).
- **HealthMCP.Stdio** — Console host using stdio transport; references the Server project so the same tool types run without duplicating code; suitable for Claude Desktop and Cursor launching a local process.
- **HealthMCP.AgentClient** — Semantic Kernel console app that connects to the HTTP server, discovers tools at startup, registers them on the kernel, and runs an interactive natural-language loop.

## Tech Stack

.NET 9, ASP.NET Core, Semantic Kernel 1.x, ModelContextProtocol 1.0.0, ModelContextProtocol.AspNetCore 1.0.0, Azure OpenAI, Microsoft.Data.Sqlite, FHIR R4, ICD-10, SNOMED CT, RxNorm, LOINC

## Getting Started

**Prerequisites:** .NET 9 SDK; an Azure OpenAI resource with `gpt-4o-mini` (or your chosen deployment) deployed.

1. Clone the repo and navigate to the solution root.
2. Create a `.env` file at the solution root with `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_KEY`, and `AZURE_OPENAI_DEPLOYMENT`.
3. Run the server: `dotnet run --project HealthMCP.Server`
4. Confirm health: `GET http://localhost:5100/health`
5. In a second terminal, run the agent: `dotnet run --project HealthMCP.AgentClient`
6. Try a sample query from `SAMPLE_QUERIES.md`.

## Connecting to Claude Desktop

Follow `CLAUDE_DESKTOP_CONFIG.md` in the repo root for step-by-step stdio configuration.

## Key Engineering Decisions

**MCP instead of custom APIs** — A single protocol covers discovery, schemas, and invocation; MCP-capable clients work without per-app HTTP contracts or SDKs.

**HTTP/SSE and stdio** — HTTP/SSE supports deployment behind normal hosting (e.g. Azure); stdio lets desktop tools spawn the server as a child process with no open port.

**FHIR R4** — Aligns with the dominant interchange model in major EHR and cloud health stacks, so tool outputs map to concepts integrators already use.

**Read-only SQL in ClinicalQueryTools** — Only `SELECT` is accepted and the connection is read-only, so data retrieval is constrained at the tool boundary rather than relying on the model to self-police writes.
