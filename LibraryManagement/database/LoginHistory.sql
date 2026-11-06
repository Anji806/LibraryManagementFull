CREATE TABLE dbo.LoginHistory
(
    LoginLogId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoginHistory PRIMARY KEY,
    UserId INT NULL,
    Username NVARCHAR(320) NOT NULL,
    LoginDateTime DATETIME2(3) NOT NULL CONSTRAINT DF_LoginHistory_LoginDateTime DEFAULT SYSUTCDATETIME(),
    IPAddress NVARCHAR(45) NULL,
    Browser NVARCHAR(256) NULL,
    OperatingSystem NVARCHAR(256) NULL,
    LoginStatus VARCHAR(10) NOT NULL,
    FailureReason NVARCHAR(100) NULL,
    CONSTRAINT CK_LoginHistory_LoginStatus CHECK (LoginStatus IN ('Success', 'Failed')),
    CONSTRAINT CK_LoginHistory_FailureReason CHECK (LoginStatus = 'Failed' OR FailureReason IS NULL),
    CONSTRAINT FK_LoginHistory_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE SET NULL
);

CREATE INDEX IX_LoginHistory_UserId_LoginDateTime ON dbo.LoginHistory(UserId, LoginDateTime DESC);
CREATE INDEX IX_LoginHistory_Username_LoginDateTime ON dbo.LoginHistory(Username, LoginDateTime DESC);
CREATE INDEX IX_LoginHistory_LoginDateTime ON dbo.LoginHistory(LoginDateTime DESC);

-- Verification examples
SELECT TOP (100) * FROM dbo.LoginHistory ORDER BY LoginDateTime DESC;
SELECT * FROM dbo.LoginHistory WHERE Username = N'alex@example.com' ORDER BY LoginDateTime DESC;
SELECT * FROM dbo.LoginHistory WHERE LoginDateTime >= @FromUtc AND LoginDateTime < @ToUtc ORDER BY LoginDateTime DESC;
