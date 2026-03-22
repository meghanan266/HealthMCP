# Claude Desktop — HealthMCP (stdio) connection

**Stdio transport** means the MCP client (Claude Desktop) starts your server as a **child process** and talks to it over **standard input and standard output** using the MCP protocol, instead of opening an HTTP port. Use stdio when the model host should **launch and supervise** the server locally (typical for desktop apps); use HTTP/SSE when a server is already running and reachable on the network.

Add the following block to your Claude Desktop MCP configuration file (`claude_desktop_config.json`), merging it with any existing `mcpServers` entries:

```json
{
  "mcpServers": {
    "healthmcp": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/HealthMCP.Stdio"],
      "env": {}
    }
  }
}
```

Replace `path/to/HealthMCP.Stdio` with the **absolute path** to the **HealthMCP.Stdio project directory** on your machine (the folder that contains `HealthMCP.Stdio.csproj`). On Windows, use forward slashes or escaped backslashes inside JSON, e.g. `C:/Meghana/Personal projects/HealthMCP/HealthMCP.Stdio`. Ensure `dotnet` is on your PATH (same terminal where `dotnet run` works).

Claude Desktop will run `dotnet run --project <that path>`; `DatabaseSeeder.Initialize()` runs at startup so the SQLite database under the process working directory is ready before tools run.

## Tools exposed (grouped by class)

These are the MCP tools registered from the shared **HealthMCP.Server** assembly (same classes as the HTTP server). The HTTP `/health` endpoint may report `"tools": 12`; the **stdio** and **HTTP** servers both register **11** tool methods today:

**PatientTools**

- `get_patient`
- `list_patients`
- `get_patient_medications`

**ClinicalQueryTools**

- `query_clinical_data`
- `list_clinical_tables`

**FHIRTools**

- `get_fhir_resource`
- `validate_fhir_resource`
- `extract_clinical_codes`

**ClinicalAlertTools**

- `check_drug_interactions`
- `flag_abnormal_vitals`
- `evaluate_readmission_risk`
