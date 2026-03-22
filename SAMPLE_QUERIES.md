# Sample queries for HealthMCP.AgentClient

Paste these into the agent after starting **HealthMCP.Server** (`dotnet run` from `HealthMCP.Server` with port **5100**) and **HealthMCP.AgentClient** with Azure OpenAI credentials in the solution `.env`.

1. **PatientTools — list patients**  
   `List every patient with their patient ID, full name, and active conditions.`

2. **PatientTools — single patient**  
   `Show full details for patient P004 including medications.`

3. **ClinicalQueryTools — simple SELECT**  
   `Using the clinical database tools, run a SELECT that returns all rows from the patients table.`

4. **ClinicalQueryTools — JOIN / filters**  
   `Query the database: list each patient’s full name next to their diagnosis descriptions by joining patients and diagnoses on patient_id. Only include patients who have at least one diagnosis.`

5. **FHIRTools — fetch resource**  
   `Fetch the FHIR MedicationRequest resource for patient P005 and summarize the medication and RxNorm coding.`

6. **FHIRTools — validate resource**  
   `Validate this FHIR JSON (paste it as one line if needed): {"resourceType":"Patient","id":"demo-1","name":[{"family":"Test","given":["Case"]}],"gender":"female"}`

7. **ClinicalAlertTools — drug interaction**  
   `Check for a clinically significant interaction between warfarin and aspirin and explain the warning.`

8. **ClinicalAlertTools — abnormal vitals (includes CRITICAL)**  
   `Flag abnormal vitals for these readings: heart rate 35 bpm, systolic BP 125 mmHg, diastolic BP 78 mmHg, temperature 36.5 °C, oxygen saturation 88%.`

9. **ClinicalAlertTools — readmission risk**  
   `Evaluate readmission risk for patient P003 and interpret the score and tier.`

10. **Compound — multiple tools in sequence**  
    `Start with patient P002. Then pull their completed appointments in the last year from the SQL database, fetch their FHIR Condition resource, and tell me whether methotrexate and ibuprofen have a known interaction.`
