using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Text.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

// Respond to CORS preflight if needed
app.MapMethods("/api/validate", new[] { "OPTIONS" }, () => Results.Ok());

app.MapPost("/api/validate", async (HttpRequest http) =>
{
    try
    {
        string? token = null;
        string? samlXmlInput = null;
        string? x509Pem = null;

        // If JSON, parse flexible fields; otherwise treat whole body as base64 token
        if ((http.ContentType ?? "").Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(http.Body);
                var root = doc.RootElement;
                token = TryGetString(root, "Token") ?? TryGetString(root, "token");
                samlXmlInput = TryGetString(root, "SamlXml") ?? TryGetString(root, "samlXml");
                x509Pem = TryGetString(root, "X509CertPem") ?? TryGetString(root, "x509CertPem") ?? TryGetString(root, "cert") ?? TryGetString(root, "certificate");
            }
            catch (JsonException)
            {
                return Results.Ok(new ValidationResponse { IsValid = false, Reason = "Request body is not valid JSON." });
            }
        }
        else
        {
            using var reader = new StreamReader(http.Body, Encoding.UTF8, leaveOpen: false);
            var raw = await reader.ReadToEndAsync();
            token = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }

        // Support Node-style { Token: base64 } or { SamlXml, X509CertPem }
        string? xmlString = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var xmlBytes = Convert.FromBase64String(token);
                xmlString = System.Text.Encoding.UTF8.GetString(xmlBytes);
            }
            catch (FormatException)
            {
                return Results.Ok(new ValidationResponse { IsValid = false, Reason = "Token is not valid base64." });
            }
        }
        else
        {
            xmlString = samlXmlInput;
        }

        if (string.IsNullOrWhiteSpace(xmlString))
        {
            return Results.Ok(new ValidationResponse { IsValid = false, Reason = "Provide Token (base64) or SamlXml (string)." });
        }

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        xmlDoc.LoadXml(xmlString);

        var signatureNode = xmlDoc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        if (signatureNode.Count == 0)
        {
            return Results.Ok(new ValidationResponse { IsValid = false, Reason = "No XMLDSIG Signature element found." });
        }

        var signedXml = new SignedXml(xmlDoc);
        signedXml.LoadXml((XmlElement)signatureNode[0]!);
        bool isValid;
        if (!string.IsNullOrWhiteSpace(x509Pem))
        {
            var cert = LoadCertificateFromPem(x509Pem);
            isValid = signedXml.CheckSignature(cert, true);
        }
        else
        {
            // Validate using embedded KeyInfo (if present)
            isValid = signedXml.CheckSignature();
        }

        return Results.Ok(new ValidationResponse { IsValid = isValid, Reason = isValid ? null : "Signature validation failed." });
    }
    catch (Exception ex)
    {
        return Results.Ok(new ValidationResponse { IsValid = false, Reason = ex.Message });
    }
});

app.Run();

static X509Certificate2 LoadCertificateFromPem(string pem)
{
    // supports both with and without header lines
    string pemContent = pem.Replace("\r", "").Replace("\n", "\n");
    if (!pemContent.Contains("BEGIN CERTIFICATE"))
    {
        // treat as base64 DER
        var raw = Convert.FromBase64String(pemContent);
        return new X509Certificate2(raw);
    }

    var lines = pemContent.Split('\n');
    var base64 = string.Join("", lines.Where(l => !l.StartsWith("---") && !string.IsNullOrWhiteSpace(l))).Trim();
    var der = Convert.FromBase64String(base64);
    return new X509Certificate2(der);
}

static string? TryGetString(JsonElement root, string name)
{
    if (root.TryGetProperty(name, out var el))
    {
        if (el.ValueKind == JsonValueKind.String)
        {
            return el.GetString();
        }
    }
    return null;
}
record ValidationResponse
{
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
}



