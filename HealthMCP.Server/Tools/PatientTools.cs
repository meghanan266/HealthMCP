using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace HealthMCP.Server.Tools;

[McpServerToolType]
public sealed class PatientTools
{
    private static readonly List<Patient> Patients =
    [
        new(
            "P001",
            "Priya Sharma",
            "1988-04-12",
            "B+",
            ["Type 2 Diabetes", "Hypertension"],
            ["Metformin", "Lisinopril"]),
        new(
            "P002",
            "Marcus Thompson",
            "1975-09-03",
            "O-",
            ["Hypertension", "Hyperlipidemia"],
            ["Amlodipine", "Atorvastatin"]),
        new(
            "P003",
            "Elena Vasquez",
            "1992-11-20",
            "A+",
            ["Asthma", "Seasonal Allergic Rhinitis"],
            ["Albuterol (inhaler)", "Fluticasone propionate-salmeterol dry powder inhaler", "Levocetirizine"]),
        new(
            "P004",
            "James Okonkwo",
            "1968-02-14",
            "AB+",
            ["Type 2 Diabetes", "Chronic Kidney Disease (stage 3)"],
            ["Insulin glargine", "Linagliptin"]),
        new(
            "P005",
            "Aisha Rahman",
            "1954-07-29",
            "A-",
            ["Heart Failure with Reduced Ejection Fraction", "Hypertension"],
            ["Carvedilol", "Lisinopril", "Furosemide"]),
        new(
            "P006",
            "David Chen",
            "2001-01-08",
            "B-",
            ["Asthma"],
            ["Budesonide (inhaled corticosteroid)", "Albuterol (inhaler)"])
    ];

    [McpServerTool(Name = "get_patient")]
    [Description("Returns full demographic and clinical details for a patient by ID.")]
    public static string GetPatient([Description("Patient identifier, e.g. P001")] string patientId)
    {
        var p = Patients.FirstOrDefault(x => x.PatientId.Equals(patientId, StringComparison.OrdinalIgnoreCase));
        if (p is null)
            return $"Error: Patient {patientId} not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"PatientId: {p.PatientId}");
        sb.AppendLine($"FullName: {p.FullName}");
        sb.AppendLine($"DateOfBirth: {p.DateOfBirth}");
        sb.AppendLine($"BloodType: {p.BloodType}");
        sb.AppendLine("ActiveConditions:");
        foreach (var c in p.ActiveConditions)
            sb.AppendLine($"  - {c}");
        sb.AppendLine("CurrentMedications:");
        foreach (var m in p.CurrentMedications)
            sb.AppendLine($"  - {m}");
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "list_patients")]
    [Description("Lists all patients with ID, name, and active conditions.")]
    public static string ListPatients()
    {
        var sb = new StringBuilder();
        foreach (var p in Patients)
        {
            sb.AppendLine($"{p.PatientId} | {p.FullName}");
            sb.AppendLine("  ActiveConditions:");
            foreach (var c in p.ActiveConditions)
                sb.AppendLine($"    - {c}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "get_patient_medications")]
    [Description("Returns current medications for a patient, with the patient name in the header.")]
    public static string GetPatientMedications([Description("Patient identifier, e.g. P001")] string patientId)
    {
        var p = Patients.FirstOrDefault(x => x.PatientId.Equals(patientId, StringComparison.OrdinalIgnoreCase));
        if (p is null)
            return $"Error: Patient {patientId} not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Medications for {p.FullName} ({p.PatientId})");
        sb.AppendLine();
        foreach (var m in p.CurrentMedications)
            sb.AppendLine($"  - {m}");
        return sb.ToString().TrimEnd();
    }

    private sealed record Patient(
        string PatientId,
        string FullName,
        string DateOfBirth,
        string BloodType,
        List<string> ActiveConditions,
        List<string> CurrentMedications);
}
