BEGIN IMMEDIATE;

CREATE TABLE IF NOT EXISTS BookCategories (
    BookId INTEGER NOT NULL,
    CategoryId INTEGER NOT NULL,
    PRIMARY KEY (BookId, CategoryId),
    FOREIGN KEY (BookId) REFERENCES Books(BookId) ON DELETE CASCADE,
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS IX_BookCategories_CategoryId ON BookCategories(CategoryId);
INSERT OR IGNORE INTO BookCategories (BookId, CategoryId)
SELECT BookId, CategoryId FROM Books;

INSERT INTO Authors (AuthorName) SELECT 'Robert C. Martin' WHERE NOT EXISTS (SELECT 1 FROM Authors WHERE AuthorName = 'Robert C. Martin' COLLATE NOCASE);
INSERT INTO Authors (AuthorName) SELECT 'Mary Shelley' WHERE NOT EXISTS (SELECT 1 FROM Authors WHERE AuthorName = 'Mary Shelley' COLLATE NOCASE);
INSERT INTO Authors (AuthorName) SELECT 'Arthur Conan Doyle' WHERE NOT EXISTS (SELECT 1 FROM Authors WHERE AuthorName = 'Arthur Conan Doyle' COLLATE NOCASE);
INSERT INTO Authors (AuthorName) SELECT 'J. K. Rowling' WHERE NOT EXISTS (SELECT 1 FROM Authors WHERE AuthorName = 'J. K. Rowling' COLLATE NOCASE);
INSERT INTO Authors (AuthorName) SELECT 'Lewis Carroll' WHERE NOT EXISTS (SELECT 1 FROM Authors WHERE AuthorName = 'Lewis Carroll' COLLATE NOCASE);
INSERT INTO Authors (AuthorName) SELECT 'Chetan Bhagat' WHERE NOT EXISTS (SELECT 1 FROM Authors WHERE AuthorName = 'Chetan Bhagat' COLLATE NOCASE);

INSERT INTO Categories (CategoryName) SELECT 'Programming' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Programming' COLLATE NOCASE);
INSERT INTO Categories (CategoryName) SELECT 'Gothic Fiction' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Gothic Fiction' COLLATE NOCASE);
INSERT INTO Categories (CategoryName) SELECT 'Science Fiction' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Science Fiction' COLLATE NOCASE);
INSERT INTO Categories (CategoryName) SELECT 'Mystery' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Mystery' COLLATE NOCASE);
INSERT INTO Categories (CategoryName) SELECT 'Detective Fiction' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Detective Fiction' COLLATE NOCASE);
INSERT INTO Categories (CategoryName) SELECT 'Fantasy' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Fantasy' COLLATE NOCASE);
INSERT INTO Categories (CategoryName) SELECT 'Children''s Literature' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Children''s Literature' COLLATE NOCASE);
INSERT INTO Categories (CategoryName) SELECT 'Mystery & Thriller' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Mystery & Thriller' COLLATE NOCASE);

INSERT OR IGNORE INTO Books (Title, ISBN, AuthorId, CategoryId, TotalCopies, AvailableCopies, PublishedYear)
SELECT v.Title, v.ISBN, a.AuthorId, c.CategoryId, v.TotalCopies, v.AvailableCopies, v.PublishedYear
FROM (SELECT 'The Pragmatic Programmer' Title, '9780135957059' ISBN, 'Robert C. Martin' Author, 'Programming' Category, 5 TotalCopies, 4 AvailableCopies, 1999 PublishedYear
UNION ALL SELECT 'Clean Code', '9780132350884', 'Robert C. Martin', 'Programming', 5, 2, 2008
UNION ALL SELECT 'Clean Architecture', '9780134494166', 'Robert C. Martin', 'Programming', 5, 4, 2017
UNION ALL SELECT 'The Modern Prometheus', '12345678909841', 'Mary Shelley', 'Gothic Fiction', 15, 15, 2010
UNION ALL SELECT 'The Hound of the Baskervilles', '12345678909842', 'Arthur Conan Doyle', 'Mystery', 20, 20, 2009
UNION ALL SELECT 'Harry Potter and the Chamber of Secrets', '12345678909843', 'J. K. Rowling', 'Fantasy', 23, 23, 1997
UNION ALL SELECT 'Alice''s Adventures in Wonderland', '12345678909845', 'Lewis Carroll', 'Children''s Literature', 10, 10, 2004
UNION ALL SELECT 'The Girl in Room 105', '9781542040464', 'Chetan Bhagat', 'Mystery & Thriller', 1, 1, 2018) v
JOIN Authors a ON a.AuthorName = v.Author COLLATE NOCASE
JOIN Categories c ON c.CategoryName = v.Category COLLATE NOCASE;

INSERT OR IGNORE INTO BookCategories (BookId, CategoryId)
SELECT b.BookId, c.CategoryId FROM Books b JOIN (SELECT '9780135957059' ISBN, 'Programming' Category UNION ALL SELECT '9780132350884', 'Programming' UNION ALL SELECT '9780134494166', 'Programming' UNION ALL SELECT '12345678909841', 'Gothic Fiction' UNION ALL SELECT '12345678909841', 'Science Fiction' UNION ALL SELECT '12345678909842', 'Mystery' UNION ALL SELECT '12345678909842', 'Detective Fiction' UNION ALL SELECT '12345678909843', 'Fantasy' UNION ALL SELECT '12345678909843', 'Children''s Literature' UNION ALL SELECT '12345678909845', 'Children''s Literature' UNION ALL SELECT '12345678909845', 'Fantasy' UNION ALL SELECT '9781542040464', 'Mystery & Thriller') v ON v.ISBN = b.ISBN JOIN Categories c ON c.CategoryName = v.Category COLLATE NOCASE;

COMMIT;
