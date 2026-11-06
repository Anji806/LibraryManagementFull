PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Authors (
    AuthorId INTEGER PRIMARY KEY AUTOINCREMENT,
    AuthorName TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Categories (
    CategoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryName TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Users (
    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName TEXT NOT NULL,
    Email TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL,
    IsActive INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
    Address TEXT NOT NULL DEFAULT ''
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Email ON Users(Email COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS Books (
    BookId INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    ISBN TEXT NOT NULL,
    AuthorId INTEGER NOT NULL,
    CategoryId INTEGER NOT NULL,
    TotalCopies INTEGER NOT NULL,
    AvailableCopies INTEGER NOT NULL,
    PublishedYear INTEGER NOT NULL,
    PreviewIntroduction TEXT NULL,
    PreviewTheme TEXT NULL,
    PreviewAbstract TEXT NULL,
    PreviewMotto TEXT NULL,
    PreviewWhyRead TEXT NULL,
    CoverUrl TEXT NULL,
    CONSTRAINT FK_Books_Authors_AuthorId FOREIGN KEY (AuthorId) REFERENCES Authors(AuthorId) ON DELETE RESTRICT,
    CONSTRAINT FK_Books_Categories_CategoryId FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Books_ISBN ON Books(ISBN COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS IX_Books_AuthorId ON Books(AuthorId);
CREATE INDEX IF NOT EXISTS IX_Books_CategoryId ON Books(CategoryId);

CREATE TABLE IF NOT EXISTS BookCategories (
    BookId INTEGER NOT NULL,
    CategoryId INTEGER NOT NULL,
    PRIMARY KEY (BookId, CategoryId),
    CONSTRAINT FK_BookCategories_Books FOREIGN KEY (BookId) REFERENCES Books(BookId) ON DELETE CASCADE,
    CONSTRAINT FK_BookCategories_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS IX_BookCategories_CategoryId ON BookCategories(CategoryId);

CREATE TABLE IF NOT EXISTS BorrowTransactions (
    TransactionId INTEGER PRIMARY KEY AUTOINCREMENT,
    BookId INTEGER NOT NULL,
    MemberId INTEGER NULL,
    MemberName TEXT NULL,
    MemberEmail TEXT NULL,
    BorrowDate TEXT NOT NULL,
    ReturnDate TEXT NULL,
    Status TEXT NOT NULL,
    CONSTRAINT FK_BorrowTransactions_Books_BookId FOREIGN KEY (BookId) REFERENCES Books(BookId) ON DELETE RESTRICT,
    CONSTRAINT FK_BorrowTransactions_Users_MemberId FOREIGN KEY (MemberId) REFERENCES Users(UserId) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS IX_BorrowTransactions_BookId ON BorrowTransactions(BookId);
CREATE INDEX IF NOT EXISTS IX_BorrowTransactions_MemberId ON BorrowTransactions(MemberId);

CREATE TABLE IF NOT EXISTS LoginHistory (
    LoginLogId INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NULL,
    Username TEXT NOT NULL,
    LoginDateTime TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    LogoutDateTime TEXT NULL,
    SessionExpiresAt TEXT NULL,
    SessionId TEXT NULL,
    IPAddress TEXT NULL,
    Browser TEXT NULL,
    OperatingSystem TEXT NULL,
    LoginStatus TEXT NOT NULL CHECK (LoginStatus IN ('Success', 'Failed')),
    FailureReason TEXT NULL,
    SessionStatus TEXT NOT NULL DEFAULT 'Unknown',
    CONSTRAINT CK_LoginHistory_FailureReason CHECK (LoginStatus = 'Failed' OR FailureReason IS NULL),
    CONSTRAINT FK_LoginHistory_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS IX_LoginHistory_UserId_LoginDateTime ON LoginHistory(UserId, LoginDateTime DESC);
CREATE INDEX IF NOT EXISTS IX_LoginHistory_Username_LoginDateTime ON LoginHistory(Username, LoginDateTime DESC);
CREATE INDEX IF NOT EXISTS IX_LoginHistory_LoginDateTime ON LoginHistory(LoginDateTime DESC);

