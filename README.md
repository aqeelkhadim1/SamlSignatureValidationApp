# SAML XML Signature Validation (Blazor WASM + ASP.NET Core Minimal API)

This repo demonstrates validating a SAML Response XML signature using a small ASP.NET Core backend API and a Blazor WebAssembly UI.

- UI: Blazor WASM (net10)
- API: ASP.NET Core Minimal API (net8) using System.Security.Cryptography.Xml

## Why a backend?
WebAssembly cannot use the required XML DSIG APIs securely. We post the SAML Response to a minimal backend that runs the validation with .NET's SignedXml.

## Project layout
- ServerApi/ — ASP.NET Core minimal API, endpoint POST /api/validate
- Pages/ValidateToken.razor — UI page to paste base64 SAML and submit to the API
- wwwroot/appsettings.json — client-side config for API base URL

## Prerequisites
- .NET 8 SDK (for ServerApi)
- .NET 10 (RC) SDK for Blazor WASM template used here
- Windows dev cert trusted (for HTTPS, optional):
  ```powershell
  dotnet dev-certs https --trust
  ```

## Quick start
Open two terminals at the repo root SamlSignatureValidationApp/.

1) Start the API
```powershell
dotnet run --project .\ServerApi\ServerApi.csproj
```
Default dev URLs (from launchSettings):
- HTTP: http://localhost:5249
- HTTPS: https://localhost:7249

2) Start the Blazor WASM app
```powershell
dotnet run --project .\SamlSignatureValidationApp.csproj
```
The WASM app reads wwwroot/appsettings.json:
```json
{ "ServerApiBaseUrl": "http://localhost:5249" }
```
Change this if your API runs on a different port or HTTPS.

3) Validate via UI
- Navigate to the WASM URL printed in the console (e.g., http://localhost:5293).
- Open the "Validate Token" page.
- Paste your base64-encoded SAML Response into the textbox.
- Click Validate.

The page displays one of:
- "Signature is valid"
- "Invalid: <reason>"

## Validate via PowerShell (without UI)
Using a base64 file saml.txt: right now, we don't have a SAML data sample, so you can add one on your own in the project root.

You can change the name of the file to any, but by default, I have set up a command for saml.txt

```powershell
$base64Saml = Get-Content .\saml.txt -Raw
Invoke-RestMethod -Method Post -Uri http://localhost:5249/api/validate -ContentType 'application/json' -Body (@{ Token = $base64Saml } | ConvertTo-Json)
```
Response:
```json
{ "isValid": true, "reason": null }
```
