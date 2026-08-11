/*
    AI-SPEIS REAL DATABASE AUDIT
    SQL Server 2019 compatible; READ ONLY.

    Execute this script in SSMS while connected as TAITRAN\os to database ai_speis.
    The script does not create objects, write data, apply migrations, or modify Hangfire.
    Dynamic statements below contain SELECT-only queries and are used only so optional
    legacy tables/columns can be absent without aborting the audit.
*/
SET NOCOUNT ON;

DECLARE @sql nvarchar(max);

PRINT N'===== 1. DATABASE INFORMATION =====';
SELECT
    DB_NAME() AS DatabaseName,
    @@SERVERNAME AS ServerName,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductLevel')) AS ProductLevel,
    SUSER_SNAME() AS CurrentLogin,
    CURRENT_USER AS CurrentDatabaseUser;

PRINT N'===== 2. EF MIGRATION HISTORY =====';
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    SELECT CAST(0 AS bit) AS HistoryTableExists, CAST(NULL AS nvarchar(150)) AS MigrationId,
           CAST(NULL AS nvarchar(32)) AS ProductVersion;
ELSE
BEGIN
    SET @sql = N'SELECT CAST(1 AS bit) AS HistoryTableExists, MigrationId, ProductVersion
                 FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;';
    EXEC sys.sp_executesql @sql;
END;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    SELECT N'20260810173029_AddTechnicalV2RuntimeFoundation' AS MigrationId,
           N'History table missing' AS MigrationStatus;
ELSE
BEGIN
    SET @sql = N'SELECT N''20260810173029_AddTechnicalV2RuntimeFoundation'' AS MigrationId,
        CASE WHEN EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N''20260810173029_AddTechnicalV2RuntimeFoundation'')
             THEN N''Applied'' ELSE N''Not applied'' END AS MigrationStatus;';
    EXEC sys.sp_executesql @sql;
END;

PRINT N'===== 3. ALL USER TABLES AND ROW COUNTS =====';
SELECT
    s.name AS [Schema],
    t.name AS [Table],
    COALESCE(SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END), 0) AS [RowCount]
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions AS p ON p.object_id = t.object_id
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name
ORDER BY s.name, t.name;

PRINT N'===== 4. TECHNICAL V2 TABLES =====';
SELECT v.TableName,
       CASE WHEN t.object_id IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS ExistsInDatabase
FROM (VALUES
    (N'dbo.TechnicalQuestionSet'),
    (N'dbo.TechnicalSessionQuestion'),
    (N'dbo.TechnicalAnswer'),
    (N'dbo.TechnicalRoundResult')
) AS v(TableName)
LEFT JOIN sys.tables AS t
  ON t.object_id = OBJECT_ID(v.TableName, N'U');

IF OBJECT_ID(N'dbo.TechnicalQuestionSet', N'U') IS NULL
    SELECT N'dbo.TechnicalQuestionSet' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS bigint) AS TotalRows, CAST(NULL AS datetime2) AS MinCreatedAt,
           CAST(NULL AS datetime2) AS MaxCreatedAt;
ELSE
BEGIN
    SET @sql = N'SELECT N''dbo.TechnicalQuestionSet'' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
        COUNT_BIG(*) AS TotalRows, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionSet') AND name = N'CreatedAt')
             THEN N'MIN(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MinCreatedAt, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionSet') AND name = N'CreatedAt')
             THEN N'MAX(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MaxCreatedAt
        FROM dbo.TechnicalQuestionSet;';
    EXEC sys.sp_executesql @sql;
END;

IF OBJECT_ID(N'dbo.TechnicalSessionQuestion', N'U') IS NULL
    SELECT N'dbo.TechnicalSessionQuestion' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS bigint) AS TotalRows, CAST(NULL AS datetime2) AS MinCreatedAt,
           CAST(NULL AS datetime2) AS MaxCreatedAt;
ELSE
BEGIN
    SET @sql = N'SELECT N''dbo.TechnicalSessionQuestion'' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
        COUNT_BIG(*) AS TotalRows, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalSessionQuestion') AND name = N'CreatedAt')
             THEN N'MIN(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MinCreatedAt, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalSessionQuestion') AND name = N'CreatedAt')
             THEN N'MAX(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MaxCreatedAt
        FROM dbo.TechnicalSessionQuestion;';
    EXEC sys.sp_executesql @sql;
END;

IF OBJECT_ID(N'dbo.TechnicalAnswer', N'U') IS NULL
    SELECT N'dbo.TechnicalAnswer' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS bigint) AS TotalRows, CAST(NULL AS datetime2) AS MinCreatedAt,
           CAST(NULL AS datetime2) AS MaxCreatedAt;
ELSE
BEGIN
    SET @sql = N'SELECT N''dbo.TechnicalAnswer'' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
        COUNT_BIG(*) AS TotalRows, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswer') AND name = N'CreatedAt')
             THEN N'MIN(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MinCreatedAt, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswer') AND name = N'CreatedAt')
             THEN N'MAX(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MaxCreatedAt
        FROM dbo.TechnicalAnswer;';
    EXEC sys.sp_executesql @sql;
END;

IF OBJECT_ID(N'dbo.TechnicalRoundResult', N'U') IS NULL
    SELECT N'dbo.TechnicalRoundResult' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS bigint) AS TotalRows, CAST(NULL AS datetime2) AS MinCreatedAt,
           CAST(NULL AS datetime2) AS MaxCreatedAt;
ELSE
BEGIN
    SET @sql = N'SELECT N''dbo.TechnicalRoundResult'' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
        COUNT_BIG(*) AS TotalRows, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalRoundResult') AND name = N'CreatedAt')
             THEN N'MIN(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MinCreatedAt, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalRoundResult') AND name = N'CreatedAt')
             THEN N'MAX(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS MaxCreatedAt
        FROM dbo.TechnicalRoundResult;';
    EXEC sys.sp_executesql @sql;
END;

PRINT N'===== 5. LEGACY TECHNICAL TABLES =====';
IF OBJECT_ID(N'dbo.TechnicalQuestionAttempt', N'U') IS NULL
    SELECT N'dbo.TechnicalQuestionAttempt' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS bigint) AS TotalRows, CAST(NULL AS bigint) AS DistinctInterviewSessionId,
           CAST(NULL AS bigint) AS DistinctQuestionId, CAST(NULL AS datetime2) AS EarliestCreatedAt,
           CAST(NULL AS datetime2) AS LatestCreatedAt;
ELSE
BEGIN
    SET @sql = N'SELECT N''dbo.TechnicalQuestionAttempt'' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
        COUNT_BIG(*) AS TotalRows, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name = N'InterviewSessionId')
             THEN N'COUNT(DISTINCT InterviewSessionId)' ELSE N'CAST(NULL AS bigint)' END + N' AS DistinctInterviewSessionId, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name = N'QuestionId')
             THEN N'COUNT(DISTINCT QuestionId)' ELSE N'CAST(NULL AS bigint)' END + N' AS DistinctQuestionId, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name = N'CreatedAt')
             THEN N'MIN(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS EarliestCreatedAt, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name = N'CreatedAt')
             THEN N'MAX(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS LatestCreatedAt
        FROM dbo.TechnicalQuestionAttempt;';
    EXEC sys.sp_executesql @sql;
END;

IF OBJECT_ID(N'dbo.TechnicalAnswerEvaluation', N'U') IS NULL
    SELECT N'dbo.TechnicalAnswerEvaluation' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS bigint) AS TotalRows, CAST(NULL AS bigint) AS DistinctAttemptId,
           CAST(NULL AS datetime2) AS EarliestCreatedAt, CAST(NULL AS datetime2) AS LatestCreatedAt;
ELSE
BEGIN
    SET @sql = N'SELECT N''dbo.TechnicalAnswerEvaluation'' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
        COUNT_BIG(*) AS TotalRows, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswerEvaluation') AND name = N'AttemptId')
             THEN N'COUNT(DISTINCT AttemptId)' ELSE N'CAST(NULL AS bigint)' END + N' AS DistinctAttemptId, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswerEvaluation') AND name = N'CreatedAt')
             THEN N'MIN(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS EarliestCreatedAt, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswerEvaluation') AND name = N'CreatedAt')
             THEN N'MAX(CreatedAt)' ELSE N'CAST(NULL AS datetime2)' END + N' AS LatestCreatedAt
        FROM dbo.TechnicalAnswerEvaluation;';
    EXEC sys.sp_executesql @sql;
END;

PRINT N'===== 6. AIInteractionLog =====';
IF OBJECT_ID(N'dbo.AIInteractionLog', N'U') IS NULL
    SELECT N'dbo.AIInteractionLog' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS bigint) AS TotalRows, CAST(NULL AS bigint) AS AttemptIdNull,
           CAST(NULL AS bigint) AS AttemptIdNotNull, CAST(NULL AS bigint) AS DistinctAttemptId,
           CAST(NULL AS bigint) AS OrphanAttemptId;
ELSE
BEGIN
    SET @sql = N'SELECT N''dbo.AIInteractionLog'' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
        COUNT_BIG(*) AS TotalRows, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AIInteractionLog') AND name = N'AttemptId')
             THEN N'SUM(CASE WHEN AttemptId IS NULL THEN 1 ELSE 0 END)' ELSE N'CAST(NULL AS bigint)' END + N' AS AttemptIdNull, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AIInteractionLog') AND name = N'AttemptId')
             THEN N'SUM(CASE WHEN AttemptId IS NOT NULL THEN 1 ELSE 0 END)' ELSE N'CAST(NULL AS bigint)' END + N' AS AttemptIdNotNull, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AIInteractionLog') AND name = N'AttemptId')
             THEN N'COUNT(DISTINCT AttemptId)' ELSE N'CAST(NULL AS bigint)' END + N' AS DistinctAttemptId, ' +
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AIInteractionLog') AND name = N'AttemptId')
                  AND OBJECT_ID(N'dbo.TechnicalQuestionAttempt', N'U') IS NOT NULL
                  AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name = N'Id')
             THEN N'(SELECT COUNT_BIG(*) FROM dbo.AIInteractionLog AS l LEFT JOIN dbo.TechnicalQuestionAttempt AS a ON a.Id = l.AttemptId WHERE l.AttemptId IS NOT NULL AND a.Id IS NULL)'
             ELSE N'CAST(NULL AS bigint)' END + N' AS OrphanAttemptId
        FROM dbo.AIInteractionLog;';
    EXEC sys.sp_executesql @sql;
END;

PRINT N'===== 7. INTERVIEWSESSION TECHNICAL COLUMNS =====';
IF OBJECT_ID(N'dbo.InterviewSession', N'U') IS NULL
    SELECT N'dbo.InterviewSession' AS TableName, CAST(0 AS bit) AS ExistsInDatabase,
           CAST(NULL AS sysname) AS ColumnName, CAST(NULL AS sysname) AS DataType,
           CAST(NULL AS bit) AS IsNullable, CAST(NULL AS bigint) AS NonNullCount;
ELSE
BEGIN
    SELECT N'dbo.InterviewSession' AS TableName, CAST(1 AS bit) AS ExistsInDatabase,
           c.name AS ColumnName, ty.name AS DataType, c.is_nullable AS IsNullable,
           CAST(NULL AS bigint) AS NonNullCount
    FROM sys.columns AS c
    JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.InterviewSession') AND c.name LIKE N'Technical%'
    ORDER BY c.column_id;

    SELECT @sql = STUFF((
        SELECT N' UNION ALL SELECT N''' + REPLACE(c.name, N'''', N'''''') + N''' AS ColumnName, COUNT_BIG(' + QUOTENAME(c.name) + N') AS NonNullCount FROM dbo.InterviewSession'
        FROM sys.columns AS c
        WHERE c.object_id = OBJECT_ID(N'dbo.InterviewSession') AND c.name LIKE N'Technical%'
        ORDER BY c.column_id
        FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 11, N'');
    IF @sql IS NOT NULL
        EXEC sys.sp_executesql @sql;
END;

PRINT N'===== 8. FOREIGN KEYS =====';
SELECT
    rs.name + N'.' + rt.name AS Parent,
    rc.name AS ParentColumn,
    ps.name + N'.' + pt.name AS Child,
    pc.name AS ChildColumn,
    fk.delete_referential_action_desc AS OnDelete
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
JOIN sys.columns AS pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
JOIN sys.columns AS rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
WHERE (pt.name IN (N'InterviewSession', N'TechnicalQuestionSet', N'TechnicalSessionQuestion', N'TechnicalAnswer', N'TechnicalRoundResult', N'TechnicalQuestionAttempt', N'TechnicalAnswerEvaluation', N'AIInteractionLog', N'Question')
    OR rt.name IN (N'InterviewSession', N'TechnicalQuestionSet', N'TechnicalSessionQuestion', N'TechnicalAnswer', N'TechnicalRoundResult', N'TechnicalQuestionAttempt', N'TechnicalAnswerEvaluation', N'AIInteractionLog', N'Question'))
ORDER BY Parent, Child, fkc.constraint_column_id;

PRINT N'===== 9. INDEXES =====';
SELECT
    s.name + N'.' + t.name AS [Table],
    i.name AS [Index],
    i.is_unique AS IsUnique,
    i.is_primary_key AS IsPrimaryKey,
    i.has_filter AS IsFiltered,
    i.filter_definition AS FilterDefinition,
    STUFF((SELECT N', ' + QUOTENAME(c2.name)
           FROM sys.index_columns AS ic2
           JOIN sys.columns AS c2 ON c2.object_id = ic2.object_id AND c2.column_id = ic2.column_id
           WHERE ic2.object_id = i.object_id AND ic2.index_id = i.index_id AND ic2.key_ordinal > 0
           ORDER BY ic2.key_ordinal, ic2.index_column_id
           FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 2, N'') AS [Columns]
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE t.name IN (N'TechnicalQuestionSet', N'TechnicalSessionQuestion', N'TechnicalAnswer', N'TechnicalRoundResult', N'TechnicalQuestionAttempt', N'TechnicalAnswerEvaluation')
  AND i.index_id > 0
ORDER BY [Table], [Index];

PRINT N'===== 10. UNIQUE CONSTRAINTS =====';
WITH Requirements AS
(
    SELECT * FROM (VALUES
        (N'TechnicalQuestionSet', N'InterviewSessionId', N'InterviewSessionId'),
        (N'TechnicalSessionQuestion', N'QuestionSetId + QuestionOrder', N'QuestionSetId|QuestionOrder'),
        (N'TechnicalAnswer', N'SessionQuestionId', N'SessionQuestionId'),
        (N'TechnicalAnswer', N'filtered SubmissionIdempotencyKey', N'SubmissionIdempotencyKey'),
        (N'TechnicalRoundResult', N'InterviewSessionId', N'InterviewSessionId')
    ) AS r(TableName, Requirement, RequiredColumns)
), UniqueIndexColumns AS
(
    SELECT i.object_id, i.index_id, t.name AS TableName, i.is_unique, i.has_filter,
           STUFF((SELECT N'|' + c2.name
                  FROM sys.index_columns ic2
                  JOIN sys.columns c2 ON c2.object_id = ic2.object_id AND c2.column_id = ic2.column_id
                  WHERE ic2.object_id = i.object_id AND ic2.index_id = i.index_id AND ic2.key_ordinal > 0
                  ORDER BY ic2.key_ordinal
                  FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N'') AS RequiredColumns
    FROM sys.indexes i
    JOIN sys.tables t ON t.object_id = i.object_id
    WHERE i.is_unique = 1 AND i.index_id > 0
)
SELECT r.TableName, r.Requirement,
       CASE WHEN r.Requirement = N'filtered SubmissionIdempotencyKey'
                  AND NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.' + r.TableName) AND c.name = N'SubmissionIdempotencyKey')
            THEN CAST(0 AS bit)
            WHEN EXISTS (SELECT 1 FROM UniqueIndexColumns u WHERE u.TableName = r.TableName AND u.RequiredColumns = r.RequiredColumns
                         AND (r.Requirement <> N'filtered SubmissionIdempotencyKey' OR u.has_filter = 1))
            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS ConstraintExists,
       CASE WHEN r.Requirement = N'filtered SubmissionIdempotencyKey'
                 AND EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.' + r.TableName) AND c.name = N'SubmissionIdempotencyKey')
            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS RequirementApplicable
FROM Requirements r
ORDER BY r.TableName, r.Requirement;

PRINT N'===== 11. ORPHAN CHECKS =====';
IF OBJECT_ID(N'dbo.TechnicalQuestionAttempt', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.InterviewSession', N'U') IS NOT NULL
   AND (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name IN (N'Id', N'InterviewSessionId')) = 2
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.InterviewSession') AND name = N'Id')
BEGIN
    SET @sql = N'SELECT N''TechnicalQuestionAttempt without InterviewSession'' AS CheckName, COUNT_BIG(*) AS OrphanCount
                FROM dbo.TechnicalQuestionAttempt a LEFT JOIN dbo.InterviewSession s ON s.Id = a.InterviewSessionId
                WHERE a.InterviewSessionId IS NOT NULL AND s.Id IS NULL;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'TechnicalQuestionAttempt without InterviewSession' AS CheckName, CAST(NULL AS bigint) AS OrphanCount;
IF OBJECT_ID(N'dbo.TechnicalAnswerEvaluation', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.TechnicalQuestionAttempt', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswerEvaluation') AND name = N'AttemptId')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name = N'Id')
BEGIN
    SET @sql = N'SELECT N''TechnicalAnswerEvaluation without TechnicalQuestionAttempt'' AS CheckName, COUNT_BIG(*) AS OrphanCount
                FROM dbo.TechnicalAnswerEvaluation e LEFT JOIN dbo.TechnicalQuestionAttempt a ON a.Id = e.AttemptId
                WHERE e.AttemptId IS NOT NULL AND a.Id IS NULL;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'TechnicalAnswerEvaluation without TechnicalQuestionAttempt' AS CheckName, CAST(NULL AS bigint) AS OrphanCount;
IF OBJECT_ID(N'dbo.TechnicalSessionQuestion', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.TechnicalQuestionSet', N'U') IS NOT NULL
   AND (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalSessionQuestion') AND name IN (N'Id', N'QuestionSetId')) = 2
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionSet') AND name = N'Id')
BEGIN
    SET @sql = N'SELECT N''TechnicalSessionQuestion without TechnicalQuestionSet'' AS CheckName, COUNT_BIG(*) AS OrphanCount
                FROM dbo.TechnicalSessionQuestion q LEFT JOIN dbo.TechnicalQuestionSet s ON s.Id = q.QuestionSetId
                WHERE q.QuestionSetId IS NOT NULL AND s.Id IS NULL;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'TechnicalSessionQuestion without TechnicalQuestionSet' AS CheckName, CAST(NULL AS bigint) AS OrphanCount;
IF OBJECT_ID(N'dbo.TechnicalAnswer', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.TechnicalSessionQuestion', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswer') AND name = N'SessionQuestionId')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalSessionQuestion') AND name = N'Id')
BEGIN
    SET @sql = N'SELECT N''TechnicalAnswer without TechnicalSessionQuestion'' AS CheckName, COUNT_BIG(*) AS OrphanCount
                FROM dbo.TechnicalAnswer a LEFT JOIN dbo.TechnicalSessionQuestion q ON q.Id = a.SessionQuestionId
                WHERE a.SessionQuestionId IS NOT NULL AND q.Id IS NULL;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'TechnicalAnswer without TechnicalSessionQuestion' AS CheckName, CAST(NULL AS bigint) AS OrphanCount;
IF OBJECT_ID(N'dbo.TechnicalRoundResult', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.InterviewSession', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalRoundResult') AND name = N'InterviewSessionId')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.InterviewSession') AND name = N'Id')
BEGIN
    SET @sql = N'SELECT N''TechnicalRoundResult without InterviewSession'' AS CheckName, COUNT_BIG(*) AS OrphanCount
                FROM dbo.TechnicalRoundResult r LEFT JOIN dbo.InterviewSession s ON s.Id = r.InterviewSessionId
                WHERE r.InterviewSessionId IS NOT NULL AND s.Id IS NULL;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'TechnicalRoundResult without InterviewSession' AS CheckName, CAST(NULL AS bigint) AS OrphanCount;
IF OBJECT_ID(N'dbo.AIInteractionLog', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.TechnicalQuestionAttempt', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AIInteractionLog') AND name = N'AttemptId')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionAttempt') AND name = N'Id')
BEGIN
    SET @sql = N'SELECT N''AIInteractionLog with missing TechnicalQuestionAttempt'' AS CheckName, COUNT_BIG(*) AS OrphanCount
                FROM dbo.AIInteractionLog l LEFT JOIN dbo.TechnicalQuestionAttempt a ON a.Id = l.AttemptId
                WHERE l.AttemptId IS NOT NULL AND a.Id IS NULL;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'AIInteractionLog with missing TechnicalQuestionAttempt' AS CheckName, CAST(NULL AS bigint) AS OrphanCount;

PRINT N'===== 12. DUPLICATE CHECKS =====';
IF OBJECT_ID(N'dbo.TechnicalQuestionSet', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalQuestionSet') AND name = N'InterviewSessionId')
BEGIN
    SET @sql = N'SELECT N''TechnicalQuestionSet.InterviewSessionId'' AS DuplicateKey, InterviewSessionId, COUNT_BIG(*) AS [RowCount]
                FROM dbo.TechnicalQuestionSet GROUP BY InterviewSessionId HAVING COUNT_BIG(*) > 1;';
    EXEC sys.sp_executesql @sql;
END
IF OBJECT_ID(N'dbo.TechnicalRoundResult', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalRoundResult') AND name = N'InterviewSessionId')
BEGIN
    SET @sql = N'SELECT N''TechnicalRoundResult.InterviewSessionId'' AS DuplicateKey, InterviewSessionId, COUNT_BIG(*) AS [RowCount]
                FROM dbo.TechnicalRoundResult GROUP BY InterviewSessionId HAVING COUNT_BIG(*) > 1;';
    EXEC sys.sp_executesql @sql;
END
IF OBJECT_ID(N'dbo.TechnicalSessionQuestion', N'U') IS NOT NULL
   AND (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalSessionQuestion') AND name IN (N'QuestionSetId', N'QuestionOrder')) = 2
BEGIN
    SET @sql = N'SELECT N''TechnicalSessionQuestion.QuestionSetId + QuestionOrder'' AS DuplicateKey, QuestionSetId, QuestionOrder, COUNT_BIG(*) AS [RowCount]
                FROM dbo.TechnicalSessionQuestion GROUP BY QuestionSetId, QuestionOrder HAVING COUNT_BIG(*) > 1;';
    EXEC sys.sp_executesql @sql;
END
IF OBJECT_ID(N'dbo.TechnicalAnswer', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TechnicalAnswer') AND name = N'SessionQuestionId')
BEGIN
    SET @sql = N'SELECT N''TechnicalAnswer.SessionQuestionId'' AS DuplicateKey, SessionQuestionId, COUNT_BIG(*) AS [RowCount]
                FROM dbo.TechnicalAnswer GROUP BY SessionQuestionId HAVING COUNT_BIG(*) > 1;';
    EXEC sys.sp_executesql @sql;
END

PRINT N'===== 13. HANGFIRE ROW COUNTS =====';
SELECT N'HangFire.' + v.TableName AS [Table],
       CASE WHEN t.object_id IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS ExistsInDatabase,
       COALESCE(SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END), 0) AS [RowCount]
FROM (VALUES (N'AggregatedCounter'), (N'Counter'), (N'Hash'), (N'Job'), (N'JobParameter'),
             (N'JobQueue'), (N'List'), (N'Schema'), (N'Server'), (N'Set'), (N'State')) AS v(TableName)
LEFT JOIN sys.tables t ON t.schema_id = SCHEMA_ID(N'HangFire') AND t.name = v.TableName
LEFT JOIN sys.partitions p ON p.object_id = t.object_id
GROUP BY v.TableName, t.object_id
ORDER BY v.TableName;

IF OBJECT_ID(N'HangFire.Server', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'HangFire.Server') AND name = N'LastHeartbeat')
BEGIN
    SET @sql = N'SELECT N''Active servers (heartbeat within 5 minutes)'' AS CheckName, COUNT_BIG(*) AS [RowCount]
                FROM HangFire.Server WHERE LastHeartbeat >= DATEADD(MINUTE, -5, GETUTCDATE());';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'Active servers (heartbeat within 5 minutes)' AS CheckName, CAST(NULL AS bigint) AS [RowCount];
IF OBJECT_ID(N'HangFire.JobQueue', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'HangFire.JobQueue') AND name = N'FetchedAt')
BEGIN
    SET @sql = N'SELECT N''Queued jobs'' AS CheckName, COUNT_BIG(*) AS [RowCount] FROM HangFire.JobQueue WHERE FetchedAt IS NULL;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'Queued jobs' AS CheckName, CAST(NULL AS bigint) AS [RowCount];
IF OBJECT_ID(N'HangFire.Set', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'HangFire.Set') AND name = N'Key')
BEGIN
    SET @sql = N'SELECT N''Scheduled jobs (Set key schedule)'' AS CheckName, COUNT_BIG(*) AS [RowCount] FROM HangFire.[Set] WHERE [Key] = N''schedule'';';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT N'Scheduled jobs (Set key schedule)' AS CheckName, CAST(NULL AS bigint) AS [RowCount];
IF OBJECT_ID(N'HangFire.State', N'U') IS NOT NULL AND OBJECT_ID(N'HangFire.Job', N'U') IS NOT NULL
   AND (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'HangFire.State') AND name IN (N'Id', N'Name')) = 2
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'HangFire.Job') AND name = N'StateId')
BEGIN
    SET @sql = N'SELECT s.Name AS StateName, COUNT_BIG(*) AS [RowCount]
                 FROM HangFire.Job AS j JOIN HangFire.State AS s ON s.Id = j.StateId
                 WHERE s.Name IN (N''Processing'', N''Failed'') GROUP BY s.Name ORDER BY s.Name;';
    EXEC sys.sp_executesql @sql;
END
ELSE SELECT CAST(NULL AS nvarchar(50)) AS StateName, CAST(NULL AS bigint) AS [RowCount];

PRINT N'===== 14. HISTORICAL / UNKNOWN TABLES =====';
WITH KnownTables AS
(
    SELECT TableName FROM (VALUES
        (N'__EFMigrationsHistory'), (N'InterviewSession'), (N'Question'),
        (N'TechnicalQuestionSet'), (N'TechnicalSessionQuestion'), (N'TechnicalAnswer'), (N'TechnicalRoundResult'),
        (N'TechnicalQuestionAttempt'), (N'TechnicalAnswerEvaluation'), (N'AIInteractionLog'),
        (N'AggregatedCounter'), (N'Counter'), (N'Hash'), (N'Job'), (N'JobParameter'), (N'JobQueue'),
        (N'List'), (N'Schema'), (N'Server'), (N'Set'), (N'State')
    ) AS k(TableName)
)
SELECT s.name AS [Schema], t.name AS [Table]
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND NOT (s.name = N'HangFire' AND EXISTS (SELECT 1 FROM KnownTables k WHERE k.TableName = t.name))
  AND NOT (s.name = N'dbo' AND EXISTS (SELECT 1 FROM KnownTables k WHERE k.TableName = t.name));

PRINT N'===== 15. SCHEMA HEALTH SUMMARY =====';
SELECT N'Existing V2 tables' AS SummaryItem, v.TableName
FROM (VALUES (N'TechnicalQuestionSet'), (N'TechnicalSessionQuestion'), (N'TechnicalAnswer'), (N'TechnicalRoundResult')) AS v(TableName)
WHERE OBJECT_ID(N'dbo.' + v.TableName, N'U') IS NOT NULL;
SELECT N'Missing expected V2 tables' AS SummaryItem, v.TableName
FROM (VALUES (N'TechnicalQuestionSet'), (N'TechnicalSessionQuestion'), (N'TechnicalAnswer'), (N'TechnicalRoundResult')) AS v(TableName)
WHERE OBJECT_ID(N'dbo.' + v.TableName, N'U') IS NULL;
SELECT N'Existing legacy Technical tables' AS SummaryItem, v.TableName
FROM (VALUES (N'TechnicalQuestionAttempt'), (N'TechnicalAnswerEvaluation')) AS v(TableName)
WHERE OBJECT_ID(N'dbo.' + v.TableName, N'U') IS NOT NULL;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    SELECT N'Migration state' AS SummaryItem, CAST(NULL AS nvarchar(150)) AS MigrationId,
           CAST(NULL AS nvarchar(32)) AS ProductVersion;
ELSE
BEGIN
    SET @sql = N'SELECT N''Migration state'' AS SummaryItem, MigrationId, ProductVersion
                 FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;';
    EXEC sys.sp_executesql @sql;
END;

PRINT N'===== END: READ-ONLY AUDIT =====';
