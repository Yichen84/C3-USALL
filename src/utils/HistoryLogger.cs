using System.IO;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.Data.Sqlite;
using CsvHelper;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.utils
{
    public static class SQLiteHistoryLogger
    {
        public static readonly string CONNECTION_STRING = "Data Source=translation_history.db;";
        public const string FeatureTypeClearBridge = "ClearBridge";
        public const string FeatureTypeLiveCaptions = "Live Captions";
        public const string FeatureTypeOcr = "OCR";
        public const string FeatureTypeOcrTranslation = "OCR Translation";
        public const string FeatureTypeOcrSummary = "OCR Summary";
        public const string FeatureTypeClearBridgeOcr = "ClearBridge OCR";
        public const string FeatureTypeClearBridgeCaptionAnalysis = "ClearBridge Caption Analysis";
        public const string FeatureTypeTranslation = "Translation";

        private static SqliteConnection _sharedConnection;
        private static readonly object _connectionLock = new object();

        static SQLiteHistoryLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            GetConnection();

            using (var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS TranslationHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT,
                    SourceText TEXT,
                    TranslatedText TEXT,
                    TargetLanguage TEXT,
                    ApiUsed TEXT,
                    FeatureType TEXT
                );", GetConnection()))
            {
                command.ExecuteNonQuery();
            }

            EnsureColumnExists("TranslationHistory", "FeatureType", "TEXT");
            EnsureColumnExists("TranslationHistory", "InputType", "TEXT");
            EnsureColumnExists("TranslationHistory", "OcrEngine", "TEXT");
            EnsureColumnExists("TranslationHistory", "OcrWasCloudBased", "INTEGER");
            EnsureColumnExists("TranslationHistory", "OcrTextEdited", "INTEGER");
            NormalizeMissingTranslationFeatureTypes();

            using (var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS ClearBridgeHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT,
                    SourceText TEXT,
                    Summary TEXT,
                    Priority TEXT,
                    ActionsJson TEXT,
                    OutputLanguage TEXT,
                    ProviderName TEXT,
                    IsMock INTEGER,
                    FeatureType TEXT,
                    ResultJson TEXT,
                    InputType TEXT,
                    OcrEngine TEXT,
                    OcrWasCloudBased INTEGER,
                    OcrTextEdited INTEGER
                );", GetConnection()))
            {
                command.ExecuteNonQuery();
            }

            EnsureColumnExists("ClearBridgeHistory", "InputType", "TEXT");
            EnsureColumnExists("ClearBridgeHistory", "OcrEngine", "TEXT");
            EnsureColumnExists("ClearBridgeHistory", "OcrWasCloudBased", "INTEGER");
            EnsureColumnExists("ClearBridgeHistory", "OcrTextEdited", "INTEGER");
            EnsureColumnExists("ClearBridgeHistory", "AnalysisScope", "TEXT");
            EnsureColumnExists("ClearBridgeHistory", "RangeStart", "INTEGER");
            EnsureColumnExists("ClearBridgeHistory", "RangeEnd", "INTEGER");
            EnsureColumnExists("ClearBridgeHistory", "OriginalSentenceCount", "INTEGER");
            EnsureColumnExists("ClearBridgeHistory", "ProcessedSentenceCount", "INTEGER");
            EnsureColumnExists("ClearBridgeHistory", "SelectedCharacterCount", "INTEGER");
            EnsureColumnExists("ClearBridgeHistory", "UserConfirmed", "INTEGER");
        }

        private static void NormalizeMissingTranslationFeatureTypes()
        {
            using (var command = new SqliteCommand(@"
                UPDATE TranslationHistory
                SET FeatureType = @FeatureType
                WHERE FeatureType IS NULL OR FeatureType = ''",
                GetConnection()))
            {
                command.Parameters.AddWithValue("@FeatureType", FeatureTypeLiveCaptions);
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureColumnExists(string tableName, string columnName, string columnType)
        {
            using (var command = new SqliteCommand($"PRAGMA table_info({tableName})", GetConnection()))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            using (var command = new SqliteCommand(
                $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}",
                GetConnection()))
            {
                command.ExecuteNonQuery();
            }
        }

        private static SqliteConnection GetConnection()
        {
            lock (_connectionLock)
            {
                if (_sharedConnection == null)
                {
                    _sharedConnection = new SqliteConnection(CONNECTION_STRING);
                    _sharedConnection.Open();
                }
                else if (_sharedConnection.State != System.Data.ConnectionState.Open)
                {
                    try
                    {
                        _sharedConnection.Open();
                    }
                    catch
                    {
                        _sharedConnection.Dispose();
                        _sharedConnection = new SqliteConnection(CONNECTION_STRING);
                        _sharedConnection.Open();
                    }
                }

                return _sharedConnection;
            }
        }

        public static async Task LogTranslation(
            string sourceText,
            string translatedText,
            string targetLanguage,
            string apiUsed,
            CancellationToken token = default,
            string featureType = FeatureTypeLiveCaptions)
        {
            string insertQuery = @"
                INSERT INTO TranslationHistory (Timestamp, SourceText, TranslatedText, TargetLanguage, ApiUsed, FeatureType)
                VALUES (@Timestamp, @SourceText, @TranslatedText, @TargetLanguage, @ApiUsed, @FeatureType)";

            using (var command = new SqliteCommand(insertQuery, GetConnection()))
            {
                command.Parameters.AddWithValue("@Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                command.Parameters.AddWithValue("@SourceText", sourceText);
                command.Parameters.AddWithValue("@TranslatedText", translatedText);
                command.Parameters.AddWithValue("@TargetLanguage", targetLanguage);
                command.Parameters.AddWithValue("@ApiUsed", apiUsed);
                command.Parameters.AddWithValue("@FeatureType", featureType);
                await command.ExecuteNonQueryAsync(token);
            }
        }

        public static async Task LogClearBridgeAnalysis(
            string sourceText,
            CrisisActionAnalysisOutcome outcome,
            string outputLanguage,
            CancellationToken token = default,
            ClearBridgeInputType inputType = ClearBridgeInputType.Text,
            string ocrEngine = "",
            bool? ocrWasCloudBased = null,
            bool ocrTextEdited = false,
            string featureType = FeatureTypeClearBridge,
            string analysisScope = "",
            int? rangeStart = null,
            int? rangeEnd = null,
            int? originalSentenceCount = null,
            int? processedSentenceCount = null,
            int? selectedCharacterCount = null,
            bool? userConfirmed = null)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var actionsJson = JsonSerializer.Serialize(outcome.Result.Actions);
            var resultJson = JsonSerializer.Serialize(outcome.Result);
            var providerName = outcome.ProviderName;
            var translatedText = FormatClearBridgeSummary(outcome.Result);

            string insertQuery = @"
                INSERT INTO ClearBridgeHistory
                    (Timestamp, SourceText, Summary, Priority, ActionsJson, OutputLanguage, ProviderName, IsMock, FeatureType, ResultJson,
                     InputType, OcrEngine, OcrWasCloudBased, OcrTextEdited, AnalysisScope, RangeStart, RangeEnd,
                     OriginalSentenceCount, ProcessedSentenceCount, SelectedCharacterCount, UserConfirmed)
                VALUES
                    (@Timestamp, @SourceText, @Summary, @Priority, @ActionsJson, @OutputLanguage, @ProviderName, @IsMock, @FeatureType, @ResultJson,
                     @InputType, @OcrEngine, @OcrWasCloudBased, @OcrTextEdited, @AnalysisScope, @RangeStart, @RangeEnd,
                     @OriginalSentenceCount, @ProcessedSentenceCount, @SelectedCharacterCount, @UserConfirmed)";

            using (var command = new SqliteCommand(insertQuery, GetConnection()))
            {
                command.Parameters.AddWithValue("@Timestamp", timestamp);
                command.Parameters.AddWithValue("@SourceText", sourceText);
                command.Parameters.AddWithValue("@Summary", outcome.Result.Summary);
                command.Parameters.AddWithValue("@Priority", outcome.Result.Priority);
                command.Parameters.AddWithValue("@ActionsJson", actionsJson);
                command.Parameters.AddWithValue("@OutputLanguage", outputLanguage);
                command.Parameters.AddWithValue("@ProviderName", providerName);
                command.Parameters.AddWithValue("@IsMock", outcome.IsMock ? 1 : 0);
                command.Parameters.AddWithValue("@FeatureType", featureType);
                command.Parameters.AddWithValue("@ResultJson", resultJson);
                command.Parameters.AddWithValue("@InputType", inputType.ToString());
                command.Parameters.AddWithValue("@OcrEngine", ocrEngine);
                command.Parameters.AddWithValue(
                    "@OcrWasCloudBased",
                    ocrWasCloudBased.HasValue ? (object)(ocrWasCloudBased.Value ? 1 : 0) : DBNull.Value);
                command.Parameters.AddWithValue("@OcrTextEdited", ocrTextEdited ? 1 : 0);
                command.Parameters.AddWithValue("@AnalysisScope", analysisScope);
                command.Parameters.AddWithValue("@RangeStart", rangeStart.HasValue ? (object)rangeStart.Value : DBNull.Value);
                command.Parameters.AddWithValue("@RangeEnd", rangeEnd.HasValue ? (object)rangeEnd.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@OriginalSentenceCount",
                    originalSentenceCount.HasValue ? (object)originalSentenceCount.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@ProcessedSentenceCount",
                    processedSentenceCount.HasValue ? (object)processedSentenceCount.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@SelectedCharacterCount",
                    selectedCharacterCount.HasValue ? (object)selectedCharacterCount.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@UserConfirmed",
                    userConfirmed.HasValue ? (object)(userConfirmed.Value ? 1 : 0) : DBNull.Value);
                await command.ExecuteNonQueryAsync(token);
            }

            await LogTranslation(
                sourceText,
                translatedText,
                outputLanguage,
                outcome.IsMock ? $"{featureType} (Mock)" : $"{featureType} ({providerName})",
                token,
                featureType);
        }

        public static async Task LogOcrTranslation(
            string confirmedOcrText,
            string translationResult,
            string targetLanguage,
            string translationProvider,
            ClearBridgeInputType inputType,
            string ocrEngine,
            bool ocrWasCloudBased,
            bool ocrTextEdited,
            CancellationToken token = default)
        {
            await LogOcrTextResult(
                confirmedOcrText,
                translationResult,
                targetLanguage,
                translationProvider,
                FeatureTypeOcrTranslation,
                inputType,
                ocrEngine,
                ocrWasCloudBased,
                ocrTextEdited,
                token);
        }

        public static async Task LogOcrSummary(
            string confirmedOcrText,
            string summaryResult,
            string summaryProvider,
            ClearBridgeInputType inputType,
            string ocrEngine,
            bool ocrWasCloudBased,
            bool ocrTextEdited,
            CancellationToken token = default)
        {
            await LogOcrTextResult(
                confirmedOcrText,
                summaryResult,
                "Summary",
                summaryProvider,
                FeatureTypeOcrSummary,
                inputType,
                ocrEngine,
                ocrWasCloudBased,
                ocrTextEdited,
                token);
        }

        private static async Task LogOcrTextResult(
            string sourceText,
            string resultText,
            string targetLanguage,
            string provider,
            string featureType,
            ClearBridgeInputType inputType,
            string ocrEngine,
            bool ocrWasCloudBased,
            bool ocrTextEdited,
            CancellationToken token)
        {
            string insertQuery = @"
                INSERT INTO TranslationHistory
                    (Timestamp, SourceText, TranslatedText, TargetLanguage, ApiUsed, FeatureType,
                     InputType, OcrEngine, OcrWasCloudBased, OcrTextEdited)
                VALUES
                    (@Timestamp, @SourceText, @TranslatedText, @TargetLanguage, @ApiUsed, @FeatureType,
                     @InputType, @OcrEngine, @OcrWasCloudBased, @OcrTextEdited)";

            using (var command = new SqliteCommand(insertQuery, GetConnection()))
            {
                command.Parameters.AddWithValue("@Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                command.Parameters.AddWithValue("@SourceText", sourceText);
                command.Parameters.AddWithValue("@TranslatedText", resultText);
                command.Parameters.AddWithValue("@TargetLanguage", targetLanguage);
                command.Parameters.AddWithValue("@ApiUsed", provider);
                command.Parameters.AddWithValue("@FeatureType", featureType);
                command.Parameters.AddWithValue("@InputType", inputType.ToString());
                command.Parameters.AddWithValue("@OcrEngine", ocrEngine);
                command.Parameters.AddWithValue("@OcrWasCloudBased", ocrWasCloudBased ? 1 : 0);
                command.Parameters.AddWithValue("@OcrTextEdited", ocrTextEdited ? 1 : 0);
                await command.ExecuteNonQueryAsync(token);
            }
        }

        private static string FormatClearBridgeSummary(CrisisActionAnalysisResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[ClearBridge] {result.Title}");
            builder.AppendLine($"Priority: {result.Priority}");
            builder.AppendLine(result.Summary);

            if (result.Actions.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Actions:");
                foreach (var action in result.Actions)
                {
                    builder.Append("- ");
                    builder.AppendLine(action.Task);
                    if (!string.IsNullOrWhiteSpace(action.Deadline))
                        builder.AppendLine($"  Deadline: {action.Deadline}");
                    if (!string.IsNullOrWhiteSpace(action.Location))
                        builder.AppendLine($"  Location: {action.Location}");
                    if (action.RequiredDocuments.Count > 0)
                        builder.AppendLine($"  Required documents: {string.Join(", ", action.RequiredDocuments)}");
                }
            }

            return builder.ToString().Trim();
        }

        public static async Task<(List<TranslationHistoryEntry>, int)> LoadHistoryAsync(
            int page, int maxRow, string searchText, CancellationToken token = default)
        {
            var history = new List<TranslationHistoryEntry>();
            int totalCount = 0;
            using (var command = new SqliteCommand(@"
                SELECT COUNT(*) 
                FROM TranslationHistory
                WHERE SourceText LIKE @search OR TranslatedText LIKE @search OR FeatureType LIKE @search", GetConnection()))

            {
                command.Parameters.AddWithValue("@search", $"%{searchText}%");
                totalCount = Convert.ToInt32(await command.ExecuteScalarAsync(token));
            }

            // 计算最大页数，至少为 1
            int maxPage = Math.Max(1, (int)Math.Ceiling(totalCount / (double)maxRow));
            int offset = Math.Max(0, (page - 1) * maxRow);

            using (var command = new SqliteCommand(@"
                SELECT Timestamp, SourceText, TranslatedText, TargetLanguage, ApiUsed, FeatureType
                FROM TranslationHistory
                WHERE SourceText LIKE @search OR TranslatedText LIKE @search OR FeatureType LIKE @search
                ORDER BY Timestamp DESC
                LIMIT @maxRow OFFSET @offset", GetConnection()))

            {
                command.Parameters.AddWithValue("@search", $"%{searchText}%");
                command.Parameters.AddWithValue("@maxRow", maxRow);
                command.Parameters.AddWithValue("@offset", offset);

                using (var reader = await command.ExecuteReaderAsync(token))
                {
                    while (await reader.ReadAsync(token))
                    {
                        string unixTime = reader.GetString(reader.GetOrdinal("Timestamp"));
                        DateTime localTime;
                        try
                        {
                            localTime = DateTimeOffset.FromUnixTimeSeconds((long)Convert.ToDouble(unixTime)).LocalDateTime;
                        }
                        catch (FormatException)
                        {
                            // DEPRECATED
                            await MigrateOldTimestampFormat();
                            return await LoadHistoryAsync(page, maxRow, string.Empty);
                        }
                        history.Add(new TranslationHistoryEntry
                        {
                            Timestamp = localTime.ToString("yyyy-MM-dd HH:mm"),
                            TimestampFull = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            SourceText = reader.GetString(reader.GetOrdinal("SourceText")),
                            TranslatedText = reader.GetString(reader.GetOrdinal("TranslatedText")),
                            TargetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage")),
                            ApiUsed = reader.GetString(reader.GetOrdinal("ApiUsed")),
                            FeatureType = GetNullableString(reader, "FeatureType", FeatureTypeLiveCaptions)
                        });
                    }
                }
            }
            return (history, maxPage);
        }

        public static async Task ClearHistory(CancellationToken token = default)
        {
            string selectQuery = "DELETE FROM TranslationHistory; DELETE FROM sqlite_sequence WHERE NAME='TranslationHistory'";
            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            {
                await command.ExecuteNonQueryAsync(token);
            }
        }

        public static async Task<string> LoadLastSourceText(CancellationToken token = default)
        {
            string selectQuery = @"
                SELECT SourceText
                FROM TranslationHistory
                ORDER BY Id DESC
                LIMIT 1";

            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                if (await reader.ReadAsync(token))
                    return reader.GetString(reader.GetOrdinal("SourceText"));
                else
                    return string.Empty;
            }
        }

        public static async Task<TranslationHistoryEntry?> LoadLastTranslation(CancellationToken token = default)
        {
            string selectQuery = @"
                SELECT Timestamp, SourceText, TranslatedText, TargetLanguage, ApiUsed, FeatureType
                FROM TranslationHistory
                ORDER BY Id DESC
                LIMIT 1";

            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                if (await reader.ReadAsync(token))
                {
                    string unixTime = reader.GetString(reader.GetOrdinal("Timestamp"));
                    DateTime localTime = DateTimeOffset.FromUnixTimeSeconds((long)Convert.ToDouble(unixTime)).LocalDateTime;
                    return new TranslationHistoryEntry
                    {
                        Timestamp = localTime.ToString("yyyy-MM-dd HH:mm"),
                        TimestampFull = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        SourceText = reader.GetString(reader.GetOrdinal("SourceText")),
                        TranslatedText = reader.GetString(reader.GetOrdinal("TranslatedText")),
                        TargetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage")),
                        ApiUsed = reader.GetString(reader.GetOrdinal("ApiUsed")),
                        FeatureType = GetNullableString(reader, "FeatureType", FeatureTypeLiveCaptions)
                    };
                }
                return null;
            }
        }

        public static async Task DeleteLastTranslation(CancellationToken token = default)
        {
            using (var command = new SqliteCommand(@"
                DELETE FROM TranslationHistory
                WHERE Id IN (SELECT Id FROM TranslationHistory ORDER BY Id DESC LIMIT 1)",
                GetConnection()))
            {
                await command.ExecuteNonQueryAsync(token);
            }
        }

        public static async Task ExportToCSV(string filePath, CancellationToken token = default)
        {
            var history = new List<TranslationHistoryEntry>();

            string selectQuery = @"
                SELECT Timestamp, SourceText, TranslatedText, TargetLanguage, ApiUsed, FeatureType
                FROM TranslationHistory
                ORDER BY Timestamp DESC";

            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (await reader.ReadAsync(token))
                {
                    string unixTime = reader.GetString(reader.GetOrdinal("Timestamp"));
                    DateTime localTime = DateTimeOffset.FromUnixTimeSeconds((long)Convert.ToDouble(unixTime)).LocalDateTime;
                    history.Add(new TranslationHistoryEntry
                    {
                        Timestamp = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        TimestampFull = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        SourceText = reader.GetString(reader.GetOrdinal("SourceText")),
                        TranslatedText = reader.GetString(reader.GetOrdinal("TranslatedText")),
                        TargetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage")),
                        ApiUsed = reader.GetString(reader.GetOrdinal("ApiUsed")),
                        FeatureType = GetNullableString(reader, "FeatureType", FeatureTypeLiveCaptions)
                    });
                }
            }

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csvWriter.WriteRecordsAsync(history, token);
        }

        private static string GetNullableString(SqliteDataReader reader, string columnName, string fallback)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);
        }

        // DEPRECATED
        private static async Task MigrateOldTimestampFormat()
        {
            var records = new List<(long id, string timestamp)>();
            using (var command = new SqliteCommand("SELECT Id, Timestamp FROM TranslationHistory", GetConnection()))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    long id = reader.GetInt64(reader.GetOrdinal("Id"));
                    string timestamp = reader.GetString(reader.GetOrdinal("Timestamp"));
                    records.Add((id, timestamp));
                }
            }

            foreach (var (id, timestamp) in records)
            {
                if (DateTime.TryParse(timestamp, out DateTime dt))
                {
                    long unixTime = ((DateTimeOffset)dt).ToUnixTimeSeconds();
                    using var updateCommand = new SqliteCommand(
                        "UPDATE TranslationHistory SET Timestamp = @Timestamp WHERE Id = @Id",
                        GetConnection());
                    updateCommand.Parameters.AddWithValue("@Id", id);
                    updateCommand.Parameters.AddWithValue("@Timestamp", unixTime.ToString());
                    await updateCommand.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
