using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace HealthMCP.Server.Tools;

[McpServerToolType]
public sealed class FHIRTools
{
    private static readonly string[] CanonicalPatientIds = ["P001", "P002", "P003", "P004", "P005", "P006"];

    private static readonly string[] SupportedResourceTypes =
        ["Patient", "Condition", "MedicationRequest", "Observation"];

    private static readonly Dictionary<(string PatientId, string ResourceType), string> FhirResources = BuildFhirResources();

    [McpServerTool(Name = "get_fhir_resource")]
    [Description("Returns a stored FHIR R4 JSON resource for a patient and resource type.")]
    public static string GetFhirResource(
        [Description("Patient identifier, e.g. P001")] string patientId,
        [Description("One of: Patient, Condition, MedicationRequest, Observation")] string resourceType)
    {
        var pidIn = patientId.Trim();
        var canonicalPatient = CanonicalPatientIds.FirstOrDefault(p => p.Equals(pidIn, StringComparison.OrdinalIgnoreCase));
        if (canonicalPatient is null)
            return $"Error: Patient {patientId} not found.";

        var rtIn = resourceType.Trim();
        var canonicalType = SupportedResourceTypes.FirstOrDefault(t => t.Equals(rtIn, StringComparison.OrdinalIgnoreCase));
        if (canonicalType is null ||
            !FhirResources.TryGetValue((canonicalPatient, canonicalType), out var json))
            return $"Error: No {resourceType} resource found for patient {patientId}.";

        return json;
    }

    [McpServerTool(Name = "validate_fhir_resource")]
    [Description("Validates a FHIR JSON document for basic structure (resourceType, id, supported type).")]
    public static string ValidateFhirResource([Description("JSON string of a FHIR resource")] string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var lines = new List<string> { "JSON syntax: PASS (valid JSON)" };

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                lines.Add("Root is JSON object: FAIL (root is not an object)");
                lines.Add("Contains resourceType field: FAIL (not evaluated)");
                lines.Add("resourceType is a supported value: FAIL (not evaluated)");
                lines.Add("Contains id field: FAIL (not evaluated)");
                lines.Add("Validation result: INVALID");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add("Root is JSON object: PASS");

            if (!root.TryGetProperty("resourceType", out var rtEl) || rtEl.ValueKind != JsonValueKind.String)
            {
                lines.Add("Contains resourceType field: FAIL (missing or not a string)");
                lines.Add("resourceType is a supported value: FAIL (cannot verify)");
                lines.Add(root.TryGetProperty("id", out var idFail) && idFail.ValueKind == JsonValueKind.String
                    ? "Contains id field: PASS"
                    : "Contains id field: FAIL (missing or not a string)");
                lines.Add("Validation result: INVALID");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add("Contains resourceType field: PASS");

            var rtValue = rtEl.GetString() ?? "";
            var supported = SupportedResourceTypes.Contains(rtValue, StringComparer.Ordinal);
            lines.Add(supported
                ? $"resourceType is a supported value: PASS ({rtValue})"
                : $"resourceType is a supported value: FAIL ('{rtValue}' is not Patient, Condition, MedicationRequest, or Observation)");

            var hasId = root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String;
            lines.Add(hasId
                ? "Contains id field: PASS"
                : "Contains id field: FAIL (missing or not a string)");

            var allPass = supported && hasId;
            lines.Add(allPass ? "Validation result: VALID" : "Validation result: INVALID");
            return string.Join(Environment.NewLine, lines);
        }
        catch (JsonException ex)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"JSON syntax: FAIL ({ex.Message})",
                "Root is JSON object: FAIL (not evaluated)",
                "Contains resourceType field: FAIL (not evaluated)",
                "resourceType is a supported value: FAIL (not evaluated)",
                "Contains id field: FAIL (not evaluated)",
                "Validation result: INVALID"
            });
        }
    }

    [McpServerTool(Name = "extract_clinical_codes")]
    [Description("Walks a FHIR JSON document and lists all objects that include system and code (typical codings).")]
    public static string ExtractClinicalCodes([Description("JSON string of a FHIR resource")] string fhirJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(fhirJson);
            var found = new List<(string System, string Code, string Display)>();
            CollectSystemCodeObjects(doc.RootElement, found);

            if (found.Count == 0)
                return "No clinical codes found in this resource.";

            var sb = new StringBuilder();
            for (var i = 0; i < found.Count; i++)
            {
                var (sys, code, display) = found[i];
                sb.AppendLine($"[{i + 1}]");
                sb.AppendLine($"  system: {sys}");
                sb.AppendLine($"  code: {code}");
                sb.AppendLine($"  display: {display}");
            }

            return sb.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            return "Error: Invalid JSON provided.";
        }
    }

    private static void CollectSystemCodeObjects(JsonElement element, List<(string, string, string)> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (TryGetCodingTriple(element, out var sys, out var code, out var display))
                    found.Add((sys, code, display));
                foreach (var prop in element.EnumerateObject())
                    CollectSystemCodeObjects(prop.Value, found);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectSystemCodeObjects(item, found);
                break;
        }
    }

    private static bool TryGetCodingTriple(JsonElement obj, out string system, out string code, out string display)
    {
        system = "";
        code = "";
        display = "";

        if (obj.ValueKind != JsonValueKind.Object)
            return false;

        if (!obj.TryGetProperty("system", out var sysEl) || sysEl.ValueKind != JsonValueKind.String)
            return false;

        if (!obj.TryGetProperty("code", out var codeEl) || codeEl.ValueKind != JsonValueKind.String)
            return false;

        system = sysEl.GetString() ?? "";
        code = codeEl.GetString() ?? "";

        if (obj.TryGetProperty("display", out var dispEl) && dispEl.ValueKind == JsonValueKind.String)
            display = dispEl.GetString() ?? "";

        return true;
    }

    private static Dictionary<(string, string), string> BuildFhirResources()
    {
        var d = new Dictionary<(string, string), string>();

        void Add(string pid, string type, string json) => d[(pid, type)] = json;

        Add("P001", "Patient", """
            {
              "resourceType": "Patient",
              "id": "P001",
              "identifier": [ {
                "system": "http://hospital.example/patients",
                "value": "P001"
              } ],
              "name": [ {
                "family": "Sharma",
                "given": [ "Priya" ]
              } ],
              "birthDate": "1988-04-12",
              "gender": "female"
            }
            """);

        Add("P001", "Condition", """
            {
              "resourceType": "Condition",
              "id": "condition-p001-t2dm",
              "clinicalStatus": {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/condition-clinical",
                  "code": "active",
                  "display": "Active"
                } ]
              },
              "code": {
                "coding": [ {
                  "system": "http://snomed.info/sct",
                  "code": "44054006",
                  "display": "Diabetes mellitus type 2"
                } ]
              },
              "subject": { "reference": "Patient/P001" },
              "onsetDateTime": "2019-03-14"
            }
            """);

        Add("P001", "MedicationRequest", """
            {
              "resourceType": "MedicationRequest",
              "id": "medreq-p001-metformin",
              "status": "active",
              "intent": "order",
              "medicationCodeableConcept": {
                "coding": [ {
                  "system": "http://www.nlm.nih.gov/research/umls/rxnorm",
                  "code": "6809",
                  "display": "Metformin"
                } ]
              },
              "subject": { "reference": "Patient/P001" },
              "authoredOn": "2024-06-10"
            }
            """);

        Add("P001", "Observation", """
            {
              "resourceType": "Observation",
              "id": "obs-p001-sbp",
              "status": "final",
              "category": [ {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                  "code": "vital-signs",
                  "display": "Vital Signs"
                } ]
              } ],
              "code": {
                "coding": [ {
                  "system": "http://loinc.org",
                  "code": "8480-6",
                  "display": "Systolic blood pressure"
                } ]
              },
              "subject": { "reference": "Patient/P001" },
              "effectiveDateTime": "2025-01-14T09:15:00Z",
              "valueQuantity": {
                "value": 128,
                "unit": "mmHg",
                "system": "http://unitsofmeasure.org",
                "code": "mm[Hg]"
              }
            }
            """);

        Add("P002", "Patient", """
            {
              "resourceType": "Patient",
              "id": "P002",
              "identifier": [ {
                "system": "http://hospital.example/patients",
                "value": "P002"
              } ],
              "name": [ {
                "family": "Thompson",
                "given": [ "Marcus" ]
              } ],
              "birthDate": "1975-09-03",
              "gender": "male"
            }
            """);

        Add("P002", "Condition", """
            {
              "resourceType": "Condition",
              "id": "condition-p002-htn",
              "clinicalStatus": {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/condition-clinical",
                  "code": "active",
                  "display": "Active"
                } ]
              },
              "code": {
                "coding": [ {
                  "system": "http://snomed.info/sct",
                  "code": "38341003",
                  "display": "Hypertensive disorder, systemic arterial"
                } ]
              },
              "subject": { "reference": "Patient/P002" },
              "onsetDateTime": "2016-11-05"
            }
            """);

        Add("P002", "MedicationRequest", """
            {
              "resourceType": "MedicationRequest",
              "id": "medreq-p002-amlodipine",
              "status": "active",
              "intent": "order",
              "medicationCodeableConcept": {
                "coding": [ {
                  "system": "http://www.nlm.nih.gov/research/umls/rxnorm",
                  "code": "1777802",
                  "display": "Amlodipine 5 MG Oral Tablet"
                } ]
              },
              "subject": { "reference": "Patient/P002" },
              "authoredOn": "2024-08-22"
            }
            """);

        Add("P002", "Observation", """
            {
              "resourceType": "Observation",
              "id": "obs-p002-weight",
              "status": "final",
              "category": [ {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                  "code": "vital-signs",
                  "display": "Vital Signs"
                } ]
              } ],
              "code": {
                "coding": [ {
                  "system": "http://loinc.org",
                  "code": "29463-7",
                  "display": "Body weight"
                } ]
              },
              "subject": { "reference": "Patient/P002" },
              "effectiveDateTime": "2025-02-20T08:00:00Z",
              "valueQuantity": {
                "value": 92.4,
                "unit": "kg",
                "system": "http://unitsofmeasure.org",
                "code": "kg"
              }
            }
            """);

        Add("P003", "Patient", """
            {
              "resourceType": "Patient",
              "id": "P003",
              "identifier": [ {
                "system": "http://hospital.example/patients",
                "value": "P003"
              } ],
              "name": [ {
                "family": "Vasquez",
                "given": [ "Elena" ]
              } ],
              "birthDate": "1992-11-20",
              "gender": "female"
            }
            """);

        Add("P003", "Condition", """
            {
              "resourceType": "Condition",
              "id": "condition-p003-asthma",
              "clinicalStatus": {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/condition-clinical",
                  "code": "active",
                  "display": "Active"
                } ]
              },
              "code": {
                "coding": [ {
                  "system": "http://snomed.info/sct",
                  "code": "195967001",
                  "display": "Asthma"
                } ]
              },
              "subject": { "reference": "Patient/P003" },
              "onsetDateTime": "2020-01-18"
            }
            """);

        Add("P003", "MedicationRequest", """
            {
              "resourceType": "MedicationRequest",
              "id": "medreq-p003-albuterol",
              "status": "active",
              "intent": "order",
              "medicationCodeableConcept": {
                "coding": [ {
                  "system": "http://www.nlm.nih.gov/research/umls/rxnorm",
                  "code": "435",
                  "display": "Albuterol 0.09 MG/ACTUAT Metered Dose Inhaler"
                } ]
              },
              "subject": { "reference": "Patient/P003" },
              "authoredOn": "2024-12-01"
            }
            """);

        Add("P003", "Observation", """
            {
              "resourceType": "Observation",
              "id": "obs-p003-hr",
              "status": "final",
              "category": [ {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                  "code": "vital-signs",
                  "display": "Vital Signs"
                } ]
              } ],
              "code": {
                "coding": [ {
                  "system": "http://loinc.org",
                  "code": "8867-4",
                  "display": "Heart rate"
                } ]
              },
              "subject": { "reference": "Patient/P003" },
              "effectiveDateTime": "2024-12-05T10:30:00Z",
              "valueQuantity": {
                "value": 72,
                "unit": "/min",
                "system": "http://unitsofmeasure.org",
                "code": "/min"
              }
            }
            """);

        Add("P004", "Patient", """
            {
              "resourceType": "Patient",
              "id": "P004",
              "identifier": [ {
                "system": "http://hospital.example/patients",
                "value": "P004"
              } ],
              "name": [ {
                "family": "Okonkwo",
                "given": [ "James" ]
              } ],
              "birthDate": "1968-02-14",
              "gender": "male"
            }
            """);

        Add("P004", "Condition", """
            {
              "resourceType": "Condition",
              "id": "condition-p004-ckd",
              "clinicalStatus": {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/condition-clinical",
                  "code": "active",
                  "display": "Active"
                } ]
              },
              "code": {
                "coding": [ {
                  "system": "http://snomed.info/sct",
                  "code": "709044004",
                  "display": "Chronic kidney disease"
                } ]
              },
              "subject": { "reference": "Patient/P004" },
              "onsetDateTime": "2021-02-11"
            }
            """);

        Add("P004", "MedicationRequest", """
            {
              "resourceType": "MedicationRequest",
              "id": "medreq-p004-glargine",
              "status": "active",
              "intent": "order",
              "medicationCodeableConcept": {
                "coding": [ {
                  "system": "http://www.nlm.nih.gov/research/umls/rxnorm",
                  "code": "1158804",
                  "display": "Insulin glargine 100 UNT/ML Injectable Solution"
                } ]
              },
              "subject": { "reference": "Patient/P004" },
              "authoredOn": "2025-01-05"
            }
            """);

        Add("P004", "Observation", """
            {
              "resourceType": "Observation",
              "id": "obs-p004-glucose",
              "status": "final",
              "category": [ {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                  "code": "laboratory",
                  "display": "Laboratory"
                } ]
              } ],
              "code": {
                "coding": [ {
                  "system": "http://loinc.org",
                  "code": "2339-0",
                  "display": "Glucose [Mass/volume] in Blood"
                } ]
              },
              "subject": { "reference": "Patient/P004" },
              "effectiveDateTime": "2025-02-04T07:45:00Z",
              "valueQuantity": {
                "value": 142,
                "unit": "mg/dL",
                "system": "http://unitsofmeasure.org",
                "code": "mg/dL"
              }
            }
            """);

        Add("P005", "Patient", """
            {
              "resourceType": "Patient",
              "id": "P005",
              "identifier": [ {
                "system": "http://hospital.example/patients",
                "value": "P005"
              } ],
              "name": [ {
                "family": "Rahman",
                "given": [ "Aisha" ]
              } ],
              "birthDate": "1954-07-29",
              "gender": "female"
            }
            """);

        Add("P005", "Condition", """
            {
              "resourceType": "Condition",
              "id": "condition-p005-hf",
              "clinicalStatus": {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/condition-clinical",
                  "code": "active",
                  "display": "Active"
                } ]
              },
              "code": {
                "coding": [ {
                  "system": "http://snomed.info/sct",
                  "code": "84114007",
                  "display": "Heart failure"
                } ]
              },
              "subject": { "reference": "Patient/P005" },
              "onsetDateTime": "2019-09-16"
            }
            """);

        Add("P005", "MedicationRequest", """
            {
              "resourceType": "MedicationRequest",
              "id": "medreq-p005-carvedilol",
              "status": "active",
              "intent": "order",
              "medicationCodeableConcept": {
                "coding": [ {
                  "system": "http://www.nlm.nih.gov/research/umls/rxnorm",
                  "code": "20352",
                  "display": "Carvedilol 12.5 MG Oral Tablet"
                } ]
              },
              "subject": { "reference": "Patient/P005" },
              "authoredOn": "2024-11-18"
            }
            """);

        Add("P005", "Observation", """
            {
              "resourceType": "Observation",
              "id": "obs-p005-sbp",
              "status": "final",
              "category": [ {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                  "code": "vital-signs",
                  "display": "Vital Signs"
                } ]
              } ],
              "code": {
                "coding": [ {
                  "system": "http://loinc.org",
                  "code": "8480-6",
                  "display": "Systolic blood pressure"
                } ]
              },
              "subject": { "reference": "Patient/P005" },
              "effectiveDateTime": "2025-01-22T11:00:00Z",
              "valueQuantity": {
                "value": 118,
                "unit": "mmHg",
                "system": "http://unitsofmeasure.org",
                "code": "mm[Hg]"
              }
            }
            """);

        Add("P006", "Patient", """
            {
              "resourceType": "Patient",
              "id": "P006",
              "identifier": [ {
                "system": "http://hospital.example/patients",
                "value": "P006"
              } ],
              "name": [ {
                "family": "Chen",
                "given": [ "David" ]
              } ],
              "birthDate": "2001-01-08",
              "gender": "male"
            }
            """);

        Add("P006", "Condition", """
            {
              "resourceType": "Condition",
              "id": "condition-p006-asthma",
              "clinicalStatus": {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/condition-clinical",
                  "code": "active",
                  "display": "Active"
                } ]
              },
              "code": {
                "coding": [ {
                  "system": "http://snomed.info/sct",
                  "code": "233678006",
                  "display": "Childhood asthma"
                } ]
              },
              "subject": { "reference": "Patient/P006" },
              "onsetDateTime": "2015-08-27"
            }
            """);

        Add("P006", "MedicationRequest", """
            {
              "resourceType": "MedicationRequest",
              "id": "medreq-p006-budesonide",
              "status": "active",
              "intent": "order",
              "medicationCodeableConcept": {
                "coding": [ {
                  "system": "http://www.nlm.nih.gov/research/umls/rxnorm",
                  "code": "19831",
                  "display": "Budesonide 0.18 MG/ACTUAT Metered Dose Inhaler"
                } ]
              },
              "subject": { "reference": "Patient/P006" },
              "authoredOn": "2024-09-10"
            }
            """);

        Add("P006", "Observation", """
            {
              "resourceType": "Observation",
              "id": "obs-p006-spo2",
              "status": "final",
              "category": [ {
                "coding": [ {
                  "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                  "code": "vital-signs",
                  "display": "Vital Signs"
                } ]
              } ],
              "code": {
                "coding": [ {
                  "system": "http://loinc.org",
                  "code": "2708-6",
                  "display": "Oxygen saturation in Arterial blood by Pulse oximetry"
                } ]
              },
              "subject": { "reference": "Patient/P006" },
              "effectiveDateTime": "2024-09-18T15:20:00Z",
              "valueQuantity": {
                "value": 98,
                "unit": "%",
                "system": "http://unitsofmeasure.org",
                "code": "%"
              }
            }
            """);

        return d;
    }
}
