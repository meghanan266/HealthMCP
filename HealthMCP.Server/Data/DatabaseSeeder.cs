using Microsoft.Data.Sqlite;

namespace HealthMCP.Server.Data;

public static class DatabaseSeeder
{
    public static void Initialize()
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "clinical.db");
        var dataDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dataDir))
            Directory.CreateDirectory(dataDir);

        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        foreach (var ddl in new[]
                 {
                     """
                     CREATE TABLE IF NOT EXISTS patients (
                         patient_id TEXT PRIMARY KEY,
                         full_name TEXT NOT NULL,
                         date_of_birth TEXT NOT NULL,
                         blood_type TEXT NOT NULL
                     );
                     """,
                     """
                     CREATE TABLE IF NOT EXISTS diagnoses (
                         id INTEGER PRIMARY KEY AUTOINCREMENT,
                         patient_id TEXT NOT NULL,
                         icd10_code TEXT NOT NULL,
                         description TEXT NOT NULL,
                         diagnosed_date TEXT NOT NULL
                     );
                     """,
                     """
                     CREATE TABLE IF NOT EXISTS appointments (
                         id INTEGER PRIMARY KEY AUTOINCREMENT,
                         patient_id TEXT NOT NULL,
                         provider_name TEXT NOT NULL,
                         department TEXT NOT NULL,
                         appointment_date TEXT NOT NULL,
                         status TEXT NOT NULL
                     );
                     """,
                     """
                     CREATE TABLE IF NOT EXISTS medications (
                         id INTEGER PRIMARY KEY AUTOINCREMENT,
                         patient_id TEXT NOT NULL,
                         medication_name TEXT NOT NULL,
                         dosage TEXT NOT NULL,
                         frequency TEXT NOT NULL
                     );
                     """
                 })
        {
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = ddl;
            createCmd.ExecuteNonQuery();
        }

        foreach (var seedSql in new[]
                 {
                     """
                     INSERT OR IGNORE INTO patients (patient_id, full_name, date_of_birth, blood_type) VALUES
                     ('P001', 'Priya Sharma', '1988-04-12', 'B+'),
                     ('P002', 'Marcus Thompson', '1975-09-03', 'O-'),
                     ('P003', 'Elena Vasquez', '1992-11-20', 'A+'),
                     ('P004', 'James Okonkwo', '1968-02-14', 'AB+'),
                     ('P005', 'Aisha Rahman', '1954-07-29', 'A-'),
                     ('P006', 'David Chen', '2001-01-08', 'B-');
                     """,
                     """
                     INSERT OR IGNORE INTO diagnoses (id, patient_id, icd10_code, description, diagnosed_date) VALUES
                     (1, 'P001', 'E11.9', 'Type 2 diabetes mellitus without complications', '2019-03-14'),
                     (2, 'P001', 'I10', 'Essential (primary) hypertension', '2018-07-22'),
                     (3, 'P002', 'I10', 'Essential (primary) hypertension', '2016-11-05'),
                     (4, 'P002', 'E78.5', 'Hyperlipidemia, unspecified', '2016-11-05'),
                     (5, 'P003', 'J45.909', 'Unspecified asthma, uncomplicated', '2020-01-18'),
                     (6, 'P003', 'J30.2', 'Other seasonal allergic rhinitis', '2019-04-09'),
                     (7, 'P004', 'E11.9', 'Type 2 diabetes mellitus without complications', '2015-06-30'),
                     (8, 'P004', 'N18.3', 'Chronic kidney disease, stage 3 (moderate)', '2021-02-11'),
                     (9, 'P005', 'I50.22', 'Chronic systolic (congestive) heart failure', '2019-09-16'),
                     (10, 'P005', 'I10', 'Essential (primary) hypertension', '2014-05-03'),
                     (11, 'P006', 'J45.40', 'Moderate persistent asthma, uncomplicated', '2015-08-27');
                     """,
                     """
                     INSERT OR IGNORE INTO appointments (id, patient_id, provider_name, department, appointment_date, status) VALUES
                     (1, 'P001', 'Dr. Amara Okafor', 'Endocrinology', '2025-01-14', 'Completed'),
                     (2, 'P001', 'Dr. Kevin Walsh', 'Cardiology', '2025-03-03', 'Scheduled'),
                     (3, 'P001', 'Dr. Lisa Park', 'Primary Care', '2024-11-08', 'Completed'),
                     (4, 'P002', 'Dr. Kevin Walsh', 'Cardiology', '2025-02-20', 'Completed'),
                     (5, 'P002', 'Dr. Nina Patel', 'Primary Care', '2025-04-02', 'Scheduled'),
                     (6, 'P003', 'Dr. Robert Hayes', 'Pulmonology', '2024-12-05', 'Completed'),
                     (7, 'P003', 'Dr. Sandra Kim', 'Allergy & Immunology', '2025-01-30', 'Completed'),
                     (8, 'P003', 'Dr. Nina Patel', 'Primary Care', '2025-05-12', 'Scheduled'),
                     (9, 'P004', 'Dr. Amara Okafor', 'Endocrinology', '2025-02-04', 'Completed'),
                     (10, 'P004', 'Dr. Miguel Santos', 'Nephrology', '2025-03-19', 'Scheduled'),
                     (11, 'P004', 'Dr. Lisa Park', 'Primary Care', '2024-10-15', 'Completed'),
                     (12, 'P005', 'Dr. Kevin Walsh', 'Cardiology', '2025-01-22', 'Completed'),
                     (13, 'P005', 'Dr. James Okoro', 'Cardiology', '2025-04-10', 'Scheduled'),
                     (14, 'P006', 'Dr. Robert Hayes', 'Pulmonology', '2024-09-18', 'Completed'),
                     (15, 'P006', 'Dr. Robert Hayes', 'Pulmonology', '2025-02-28', 'Cancelled');
                     """,
                     """
                     INSERT OR IGNORE INTO medications (id, patient_id, medication_name, dosage, frequency) VALUES
                     (1, 'P001', 'Metformin', '1000 mg', 'Twice daily with meals'),
                     (2, 'P001', 'Lisinopril', '10 mg', 'Once daily'),
                     (3, 'P002', 'Amlodipine', '5 mg', 'Once daily'),
                     (4, 'P002', 'Atorvastatin', '40 mg', 'Once daily at bedtime'),
                     (5, 'P003', 'Albuterol HFA inhaler', '90 mcg per actuation', 'As needed for wheeze'),
                     (6, 'P003', 'Fluticasone-salmeterol DPI', '250/50 mcg', 'Twice daily'),
                     (7, 'P004', 'Insulin glargine', '20 units', 'Once daily at bedtime'),
                     (8, 'P004', 'Linagliptin', '5 mg', 'Once daily'),
                     (9, 'P005', 'Carvedilol', '12.5 mg', 'Twice daily'),
                     (10, 'P005', 'Furosemide', '40 mg', 'Once daily'),
                     (11, 'P006', 'Budesonide DPI', '180 mcg', 'Twice daily'),
                     (12, 'P006', 'Albuterol HFA inhaler', '90 mcg per actuation', 'As needed for symptoms');
                     """
                 })
        {
            using var seedCmd = connection.CreateCommand();
            seedCmd.CommandText = seedSql;
            seedCmd.ExecuteNonQuery();
        }
    }
}
