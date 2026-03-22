using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Server;

namespace HealthMCP.Server.Tools;

[McpServerToolType]
public sealed class ClinicalAlertTools
{
    private static readonly Dictionary<(string A, string B), string> DrugInteractions = BuildDrugInteractions();

    [McpServerTool(Name = "check_drug_interactions")]
    [Description("Checks a static reference list for clinically significant drug–drug interactions.")]
    public static string CheckDrugInteractions(
        [Description("First drug name")] string drug1,
        [Description("Second drug name")] string drug2)
    {
        var a = drug1.Trim().ToLowerInvariant();
        var b = drug2.Trim().ToLowerInvariant();
        if (a.Length == 0 || b.Length == 0)
            return $"No known interaction found between {drug1} and {drug2}. Always verify with a clinical pharmacist.";

        var key = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        if (!DrugInteractions.TryGetValue(key, out var description))
            return $"No known interaction found between {drug1} and {drug2}. Always verify with a clinical pharmacist.";

        var sb = new StringBuilder();
        sb.AppendLine("DRUG INTERACTION ALERT");
        sb.AppendLine($"Medications: {drug1.Trim()} + {drug2.Trim()}");
        sb.AppendLine($"Interaction: {description}");
        sb.AppendLine("Severity: HIGH - Consult prescribing physician before administering.");
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "flag_abnormal_vitals")]
    [Description("Flags abnormal vital signs using predefined WARNING and CRITICAL thresholds.")]
    public static string FlagAbnormalVitals(
        [Description("Heart rate (beats per minute)")] double heartRate,
        [Description("Systolic blood pressure (mmHg)")] double systolicBP,
        [Description("Diastolic blood pressure (mmHg)")] double diastolicBP,
        [Description("Body temperature (°C)")] double temperatureCelsius,
        [Description("Oxygen saturation (%, SpO2)")] double oxygenSaturation)
    {
        var blocks = new List<string>();
        var anyCritical = false;

        void AddFinding(string vitalName, double value, string normalRange, string severity, string detail)
        {
            var b = new StringBuilder();
            b.AppendLine($"{severity}: {vitalName}");
            b.AppendLine($"  Measured: {FormatValue(value)}");
            b.AppendLine($"  Normal range: {normalRange}");
            b.Append($"  {detail}");
            blocks.Add(b.ToString());
            if (severity == "CRITICAL")
                anyCritical = true;
        }

        var hr = ClassifyHeartRate(heartRate);
        if (hr is not null)
            AddFinding("Heart rate", heartRate, "60–100 bpm (typical adult resting)", hr.Value.Severity, hr.Value.Detail);

        var sbp = ClassifySystolic(systolicBP);
        if (sbp is not null)
            AddFinding("Systolic blood pressure", systolicBP, "90–140 mmHg", sbp.Value.Severity, sbp.Value.Detail);

        var dbp = ClassifyDiastolic(diastolicBP);
        if (dbp is not null)
            AddFinding("Diastolic blood pressure", diastolicBP, "60–90 mmHg", dbp.Value.Severity, dbp.Value.Detail);

        var temp = ClassifyTemperature(temperatureCelsius);
        if (temp is not null)
            AddFinding("Temperature", temperatureCelsius, "36.1–37.8 °C (oral, typical adult)", temp.Value.Severity, temp.Value.Detail);

        var spo2 = ClassifySpO2(oxygenSaturation);
        if (spo2 is not null)
            AddFinding("Oxygen saturation", oxygenSaturation, "≥95% on room air (typical adult)", spo2.Value.Severity, spo2.Value.Detail);

        if (blocks.Count == 0)
            return "All vitals within normal range.";

        var report = new StringBuilder();
        report.AppendLine("ABNORMAL VITALS ALERT");
        report.AppendLine();
        report.Append(string.Join(Environment.NewLine + Environment.NewLine, blocks));
        if (anyCritical)
        {
            report.AppendLine();
            report.AppendLine();
            report.AppendLine("ACTION REQUIRED: Notify attending physician immediately.");
        }

        return report.ToString().TrimEnd();
    }

    [McpServerTool(Name = "evaluate_readmission_risk")]
    [Description("Estimates readmission-related risk from recent completed visits and diagnosis count in the clinical database.")]
    public static string EvaluateReadmissionRisk([Description("Patient identifier, e.g. P001")] string patientId)
    {
        var pid = patientId.Trim();
        var connectionString = new SqliteConnectionStringBuilder { DataSource = ClinicalQueryTools.DbPath }.ToString();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using (var existsCmd = connection.CreateCommand())
            {
                existsCmd.CommandText = "SELECT 1 FROM patients WHERE patient_id = @p COLLATE NOCASE LIMIT 1;";
                existsCmd.Parameters.AddWithValue("@p", pid);
                var exists = existsCmd.ExecuteScalar();
                if (exists is null)
                    return $"Error: Patient {patientId} not found.";
            }

            var cutoff = DateTime.UtcNow.Date.AddDays(-365).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            int completedVisits;
            using (var visitCmd = connection.CreateCommand())
            {
                visitCmd.CommandText = """
                    SELECT COUNT(*) FROM appointments
                    WHERE patient_id = @p COLLATE NOCASE
                      AND status = 'Completed'
                      AND appointment_date >= @cutoff;
                    """;
                visitCmd.Parameters.AddWithValue("@p", pid);
                visitCmd.Parameters.AddWithValue("@cutoff", cutoff);
                completedVisits = Convert.ToInt32(visitCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            int diagnosisCount;
            using (var dxCmd = connection.CreateCommand())
            {
                dxCmd.CommandText = "SELECT COUNT(*) FROM diagnoses WHERE patient_id = @p COLLATE NOCASE;";
                dxCmd.Parameters.AddWithValue("@p", pid);
                diagnosisCount = Convert.ToInt32(dxCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            var visitPoints = Math.Min(completedVisits * 20, 60);
            var diagnosisPoints = Math.Min(diagnosisCount * 15, 45);
            var totalScore = Math.Min(visitPoints + diagnosisPoints, 100);

            string tier;
            string recommendation;
            if (totalScore <= 34)
            {
                tier = "LOW";
                recommendation = "Routine follow-up schedule recommended.";
            }
            else if (totalScore <= 64)
            {
                tier = "MODERATE";
                recommendation = "Consider scheduling a care coordination review.";
            }
            else
            {
                tier = "HIGH";
                recommendation = "Flag for case management and priority follow-up.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("READMISSION RISK ASSESSMENT");
            sb.AppendLine($"Patient ID: {pid}");
            sb.AppendLine($"Completed visits (last 365 days): {completedVisits}");
            sb.AppendLine($"Active diagnoses (count in diagnoses table): {diagnosisCount}");
            sb.AppendLine($"Risk score: {totalScore} / 100");
            sb.AppendLine($"Risk tier: {tier} (LOW: 0–34, MODERATE: 35–64, HIGH: 65–100)");
            sb.AppendLine(recommendation);
            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            return $"Database error: {ex.Message}";
        }
    }

    private static string FormatValue(double v) =>
        v.ToString("0.##", CultureInfo.InvariantCulture);

    private static (string Severity, string Detail)? ClassifyHeartRate(double hr)
    {
        if (hr < 40 || hr > 150)
            return ("CRITICAL", "Outside critical thresholds (<40 or >150 bpm).");
        if (hr < 60 || hr > 100)
            return ("WARNING", "Outside typical resting range (<60 or >100 bpm).");
        return null;
    }

    private static (string Severity, string Detail)? ClassifySystolic(double sbp)
    {
        if (sbp < 70 || sbp > 180)
            return ("CRITICAL", "Outside critical thresholds (<70 or >180 mmHg).");
        if (sbp < 90 || sbp > 140)
            return ("WARNING", "Outside common outpatient targets (<90 or >140 mmHg).");
        return null;
    }

    private static (string Severity, string Detail)? ClassifyDiastolic(double dbp)
    {
        if (dbp < 40 || dbp > 120)
            return ("CRITICAL", "Outside critical thresholds (<40 or >120 mmHg).");
        if (dbp < 60 || dbp > 90)
            return ("WARNING", "Outside typical range (<60 or >90 mmHg).");
        return null;
    }

    private static (string Severity, string Detail)? ClassifyTemperature(double c)
    {
        if (c < 35.0 || c > 39.5)
            return ("CRITICAL", "Outside critical thresholds (<35.0 or >39.5 °C).");
        if (c < 36.1 || c > 37.8)
            return ("WARNING", "Outside typical adult oral range (<36.1 or >37.8 °C).");
        return null;
    }

    private static (string Severity, string Detail)? ClassifySpO2(double spo2)
    {
        if (spo2 < 90)
            return ("CRITICAL", "Severe hypoxemia (<90%).");
        if (spo2 < 95)
            return ("WARNING", "Below usual target on room air (<95%).");
        return null;
    }

    private static Dictionary<(string A, string B), string> BuildDrugInteractions()
    {
        var d = new Dictionary<(string, string), string>();
        void Add(string x, string y, string text)
        {
            var a = x.Trim().ToLowerInvariant();
            var b = y.Trim().ToLowerInvariant();
            var key = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
            d[key] = text;
        }

        Add("warfarin", "aspirin", "Combined anticoagulant and antiplatelet effects substantially increase major bleeding risk, including GI and intracranial hemorrhage.");
        Add("metformin", "alcohol", "Alcohol increases lactate production and impairs hepatic lactate clearance, raising risk of metformin-associated lactic acidosis, especially with heavy use or acute intoxication.");
        Add("lisinopril", "potassium", "ACE inhibition reduces aldosterone; added potassium loads or supplements increase risk of life-threatening hyperkalemia.");
        Add("simvastatin", "amiodarone", "Amiodarone inhibits CYP3A4 and P-gp, markedly raising simvastatin exposure and risk of rhabdomyolysis and hepatotoxicity.");
        Add("clopidogrel", "omeprazole", "Omeprazole inhibits CYP2C19, reducing formation of clopidogrel’s active metabolite and potentially diminishing antiplatelet efficacy.");
        Add("methotrexate", "ibuprofen", "NSAIDs reduce renal clearance of methotrexate and displace protein binding, increasing risk of bone marrow suppression, mucositis, and acute kidney injury.");
        Add("fluoxetine", "tramadol", "Combined serotonergic effects increase risk of serotonin syndrome (agitation, hyperthermia, clonus, autonomic instability).");
        Add("warfarin", "ibuprofen", "NSAIDs impair platelet function and gastric mucosal defense and may alter warfarin metabolism, increasing bleeding risk.");
        Add("digoxin", "amiodarone", "Amiodarone raises digoxin levels via P-gp inhibition and renal effects, increasing risk of bradyarrhythmias and digoxin toxicity.");
        Add("ciprofloxacin", "antacids", "Polyvalent cations in antacids chelate fluoroquinolones in the GI tract, reducing absorption and antibacterial efficacy.");
        Add("lithium", "ibuprofen", "NSAIDs reduce renal lithium clearance, predisposing to lithium accumulation, tremor, confusion, and nephrogenic diabetes insipidus.");
        Add("sertraline", "tramadol", "SSRI plus tramadol increases serotonergic tone and seizure risk; monitor closely for serotonin syndrome.");
        Add("apixaban", "aspirin", "Dual antithrombotic pathways increase major bleeding risk; combination should be reserved for clear indications with risk mitigation.");
        Add("naproxen", "enalapril", "NSAID plus ACE inhibitor reduces renal perfusion and GFR, increasing risk of acute kidney injury and hyperkalemia, especially if dehydrated.");
        Add("prednisone", "ibuprofen", "Glucocorticoid plus NSAID markedly increases risk of gastric ulceration and GI bleeding.");

        return d;
    }
}
