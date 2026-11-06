/*
 Greenleaf Library: review-first preview content update.

 Run this against the configured LibraryManagementDb database. It updates only
 existing book rows. @ApplyUpdates defaults to 0: that mode validates matches,
 prints the proposed rows, and rolls back. After reviewing the output and this
 script, set @ApplyUpdates to 1 to apply the same transaction.

 Research references (content is original paraphrase, not copied description):
 Pragmatic Bookshelf (authors, first publication, topics): https://pragprog.com/titles/tpp20/the-pragmatic-programmer-20th-anniversary-edition/
 Pearson Clean Code: https://www.pearson.com/en-us/subject-catalog/p/clean-code-a-handbook-of-agile-software-craftsmanship/P200000009044/9780136083252
 O'Reilly Clean Architecture: https://www.oreilly.com/library/view/clean-architecture-a/9780134494272/
 Library of Congress Frankenstein catalog record and alternate title: https://www.loc.gov/item/53051218/
 Library of Congress on Hound serial/book publication: https://blogs.loc.gov/headlinesandheroes/2021/10/serial-fiction-part-1/
 Bloomsbury Chamber of Secrets: https://www.bloomsbury.com/ca/harry-potter-and-the-chamber-of-secrets-9781408865408/
 Morgan Library Alice first-edition catalog: https://www.themorgan.org/printed-books/246978
 Bloomsbury Chamber of Secrets: https://www.bloomsbury.com/uk/harry-potter-and-the-chamber-of-secrets-9781408894637/
 Chetan Bhagat official book page: https://www.chetanbhagat.com/books/the-girl-in-room-105/
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ApplyUpdates bit = 0; -- Review-only by default. Change to 1 after approval.
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Books', N'U') IS NULL OR OBJECT_ID(N'dbo.Authors', N'U') IS NULL
        THROW 51000, 'Expected dbo.Books and dbo.Authors tables were not found in the current database.', 1;

    IF COL_LENGTH(N'dbo.Books', N'PreviewIntroduction') IS NULL
        ALTER TABLE dbo.Books ADD PreviewIntroduction nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Books', N'PreviewTheme') IS NULL
        ALTER TABLE dbo.Books ADD PreviewTheme nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Books', N'PreviewAbstract') IS NULL
        ALTER TABLE dbo.Books ADD PreviewAbstract nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Books', N'PreviewMotto') IS NULL
        ALTER TABLE dbo.Books ADD PreviewMotto nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Books', N'PreviewWhyRead') IS NULL
        ALTER TABLE dbo.Books ADD PreviewWhyRead nvarchar(max) NULL;

    DECLARE @Content table (
        TargetTitle nvarchar(250) NOT NULL,
        ExpectedAuthor nvarchar(200) NOT NULL,
        IsFrankensteinAlias bit NOT NULL DEFAULT 0,
        PreviewIntroduction nvarchar(max) NOT NULL,
        PreviewTheme nvarchar(max) NOT NULL,
        PreviewAbstract nvarchar(max) NOT NULL,
        PreviewMotto nvarchar(max) NOT NULL,
        PreviewWhyRead nvarchar(max) NOT NULL
    );

    INSERT @Content (TargetTitle, ExpectedAuthor, IsFrankensteinAlias, PreviewIntroduction, PreviewTheme, PreviewAbstract, PreviewMotto, PreviewWhyRead)
    VALUES
    (N'The Pragmatic Programmer', N'Andrew Hunt and David Thomas', 0,
     N'Andrew Hunt and David Thomas wrote this guide for people who build and maintain software. First published in 1999 and later revised, it treats programming as a craft that depends on judgment, curiosity, communication, and continued learning.',
     N'The central idea is to take responsibility for the quality and adaptability of your work. The book encourages readers to avoid needless repetition and fragile dependencies, make useful knowledge explicit, automate routine work, and keep improving both their tools and their thinking.',
     N'A sequence of practical principles and techniques shows how developers can make software easier to change and their daily work more deliberate. Topics range from gathering requirements and shaping resilient designs to testing, debugging, collaboration, and career-long learning. Its advice is presented as habits to reason about, not a single prescribed process.',
     N'Build with care; stay ready to adapt.',
     N'Read it for memorable principles that connect everyday coding choices with the long-term health of a project. It invites experienced developers and newcomers alike to examine how they work, communicate, and respond when requirements or tools change.'),
    (N'Clean Code', N'Robert C. Martin', 0,
     N'Robert C. Martin’s software-craftsmanship handbook examines the small decisions that make source code understandable and maintainable. Published in 2008, it combines principles, examples, and discussions of improving existing code.',
     N'Its theme is that working software should also communicate its intent clearly. Names, functions, tests, and structure all affect how safely a team can understand, change, and extend a program.',
     N'The book moves from qualities associated with readable code to practical design habits and worked examples. Readers encounter ways to reduce confusion, organize responsibilities, handle errors, and use tests while refining a codebase. The emphasis is on close reading and repeated improvement rather than treating “clean” as a cosmetic style.',
     N'Write so the next change is easier to understand.',
     N'Read it to sharpen your eye for code that hides its purpose and to practice explaining design choices. Its examples encourage thoughtful review, disciplined refactoring, and shared standards that help teams maintain software over time.'),
    (N'Clean Architecture', N'Robert C. Martin', 0,
     N'In this guide to software system design, Robert C. Martin looks beyond individual functions and files to the structures that shape an application. The book presents architecture as a way to preserve useful choices and keep important business rules understandable as systems evolve.',
     N'The main theme is separation of concerns: policy should not be trapped inside details such as a database, framework, or delivery mechanism. Clear boundaries make it easier to test, replace, and adapt those details without losing the purpose of the system.',
     N'Through principles, diagrams, and examples, the book considers component organization, dependency direction, use cases, and the trade-offs behind architectural decisions. It asks readers to connect code structure with the people, requirements, and future changes a system must serve.',
     N'Keep the purpose clear as the details change.',
     N'Read it if you make decisions about application structure or want to understand why boundaries matter. It offers a vocabulary for discussing design trade-offs and helps readers evaluate whether a system can change without making its central rules harder to find.'),
    (N'The Modern Prometheus', N'Mary Shelley', 1,
     N'Mary Shelley’s Gothic novel, first published in 1818 as Frankenstein; or, The Modern Prometheus, brings Romantic-era questions about science and human responsibility into a haunting story. The title in this catalog is treated as an alternate form of that same work, not as a separate book.',
     N'The novel explores the responsibilities that accompany creation, the need for companionship, and the consequences of rejecting those one has brought into the world. It also questions ambition when the pursuit of knowledge outruns care for other people.',
     N'An ambitious young scholar pursues a daring experiment and must reckon with what follows. Told through layered accounts, the novel moves between questions of discovery, isolation, belonging, and accountability, leaving readers to consider how sympathy and neglect shape the lives of both creator and creation.',
     N'Creation carries a responsibility to care.',
     N'Read it for a landmark Gothic story whose questions remain relevant to science, invention, and social responsibility. Its layered narration and moral uncertainty reward readers who enjoy atmosphere as much as ideas.'),
    (N'The Hound of the Baskervilles', N'Arthur Conan Doyle', 0,
     N'Arthur Conan Doyle’s Sherlock Holmes mystery appeared in serial form in 1901–1902 and as a book in 1902. It brings Holmes and Dr. Watson to the bleak landscape of Dartmoor, where an old family legend shadows a present-day investigation.',
     N'The story sets rational observation against fear, rumor, and inherited belief. It also makes landscape part of the mystery: isolation and uncertainty influence what people see and how they interpret it.',
     N'When the Baskerville family faces a new threat linked to a frightening local legend, Dr. Watson is sent to keep watch and report what he discovers. Clues, suspicious movements, and the atmosphere of the moor gradually form a case for Sherlock Holmes. The preview leaves the identity and resolution of the danger for the reader.',
     N'Look closely; fear can distort the evidence.',
     N'Read it for a classic detective puzzle with a strong sense of place. Watson’s observations, Holmes’s method, and the tension between folklore and evidence make it an inviting choice for mystery and atmospheric fiction readers.'),
    (N'Harry Potter and the Chamber of Secrets', N'J. K. Rowling', 0,
     N'J. K. Rowling’s second Harry Potter novel returns readers to Hogwarts for another school year of classes, friendships, and magic. Its fantasy setting expands while Harry and his friends face a mystery that unsettles the whole school.',
     N'The story considers belonging, prejudice, courage, and the danger of judging people by their background. Loyalty and careful attention to history become important as students try to understand the threats around them.',
     N'As troubling incidents disrupt life at Hogwarts, Harry, Ron, and Hermione follow clues that connect present fears with the school’s past. Rumors and suspicion spread, and Harry must decide whom to trust while learning more about the place he calls home. This overview preserves the novel’s revelations for new readers.',
     N'Choose courage, curiosity, and loyalty when fear spreads.',
     N'Read it for a richly imagined school mystery, a close-knit friendship at its center, and a deeper look at the history of Hogwarts. It rewards readers who enjoy clues, magical details, and questions about fairness and belonging.'),
    (N'Alice''s Adventures in Wonderland', N'Lewis Carroll', 0,
     N'Lewis Carroll’s 1865 fantasy follows Alice into a world where familiar rules of language, size, manners, and logic keep shifting. The book grew from a story told to children and became a defining work of imaginative nonsense literature.',
     N'Playful wordplay and impossible encounters invite readers to question how rules are made and what happens when language is taken apart or turned upside down. Curiosity and a child’s effort to make sense of change hold the journey together.',
     N'After following an unusual rabbit, Alice enters a strange realm of changing scale and surprising conversations. Each encounter brings a new puzzle in etiquette, identity, or reasoning. Rather than following a conventional quest, the book builds a chain of comic episodes that invites readers to enjoy its inventions and make their own sense of them.',
     N'Follow your curiosity into the unexpected.',
     N'Read it for its lively imagination, memorable wordplay, and layered humor that works differently for readers of different ages. It is a compact invitation to question assumptions and enjoy language when it refuses to behave.'),
    (N'The Girl in Room 105', N'Chetan Bhagat', 0,
     N'Chetan Bhagat’s contemporary Indian thriller combines a relationship story with a mystery shaped by memory, family expectations, and the search for meaning. The author describes its concerns through the ideas of unloving and moving on.',
     N'At its heart are attachment, closure, and the difficult work of moving forward. The story also touches on religious difference and life in contemporary India, including Kashmir, while balancing suspense with humor and emotional reflection.',
     N'Keshav is still struggling to move on from Zara when a message brings him to her hostel room on the eve of her birthday. The encounter draws him into an urgent search for answers about their past and the circumstances surrounding her. A fast-moving trail of questions begins, while the central revelations remain for the reader.',
     N'Sometimes moving forward begins with learning to let go.',
     N'Read it for a brisk mystery with an emotional center and a contemporary Indian setting. Keshav’s search for answers also becomes a search for closure, purpose, and a way to face what comes next.');

    DECLARE @Matches table (TargetTitle nvarchar(250) NOT NULL, BookId int NOT NULL, CurrentAuthorId int NOT NULL);

    INSERT @Matches (TargetTitle, BookId, CurrentAuthorId)
    SELECT c.TargetTitle, b.BookId, b.AuthorId
    FROM @Content c
    INNER JOIN dbo.Books b
      ON (c.IsFrankensteinAlias = 0 AND b.Title = c.TargetTitle)
      OR (c.IsFrankensteinAlias = 1 AND b.Title IN
            (N'The Modern Prometheus', N'Frankenstein', N'Frankenstein; or, The Modern Prometheus', N'Frankenstein, or The Modern Prometheus'));

    IF EXISTS (
        SELECT c.TargetTitle
        FROM @Content c
        LEFT JOIN @Matches m ON m.TargetTitle = c.TargetTitle
        GROUP BY c.TargetTitle
        HAVING COUNT(m.BookId) <> 1
    )
    BEGIN
        SELECT c.TargetTitle, COUNT(m.BookId) AS MatchingRows
        FROM @Content c
        LEFT JOIN @Matches m ON m.TargetTitle = c.TargetTitle
        GROUP BY c.TargetTitle;
        THROW 51001, 'A target title is missing or ambiguous. No content was updated; inspect the result and adjust the title mapping.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM @Content c
        INNER JOIN @Matches m ON m.TargetTitle = c.TargetTitle
        INNER JOIN dbo.Authors a ON a.AuthorId = m.CurrentAuthorId
        WHERE c.TargetTitle <> N'The Pragmatic Programmer'
          AND a.AuthorName <> c.ExpectedAuthor
    )
        THROW 51002, 'An existing author does not match the verified title attribution. No content was updated.', 1;

    IF (SELECT COUNT(*) FROM dbo.Authors WHERE AuthorName = N'Andrew Hunt and David Thomas') > 1
        THROW 51003, 'Duplicate combined-author rows already exist. Review dbo.Authors before applying.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE AuthorName = N'Andrew Hunt and David Thomas')
        INSERT dbo.Authors (AuthorName) VALUES (N'Andrew Hunt and David Thomas');

    DECLARE @PragmaticAuthorId int = (
        SELECT AuthorId FROM dbo.Authors WHERE AuthorName = N'Andrew Hunt and David Thomas'
    );
    IF @PragmaticAuthorId IS NULL
        THROW 51004, 'Could not resolve the verified Pragmatic Programmer author row.', 1;

    UPDATE b
    SET b.AuthorId = @PragmaticAuthorId
    FROM dbo.Books b
    INNER JOIN @Matches m ON m.BookId = b.BookId
    WHERE m.TargetTitle = N'The Pragmatic Programmer';

    UPDATE b
    SET b.PreviewIntroduction = c.PreviewIntroduction,
        b.PreviewTheme = c.PreviewTheme,
        b.PreviewAbstract = c.PreviewAbstract,
        b.PreviewMotto = c.PreviewMotto,
        b.PreviewWhyRead = c.PreviewWhyRead
    FROM dbo.Books b
    INNER JOIN @Matches m ON m.BookId = b.BookId
    INNER JOIN @Content c ON c.TargetTitle = m.TargetTitle;

    SELECT b.BookId, b.Title, a.AuthorName, b.PublishedYear,
           b.PreviewIntroduction, b.PreviewTheme, b.PreviewAbstract, b.PreviewMotto, b.PreviewWhyRead
    FROM dbo.Books b
    INNER JOIN @Matches m ON m.BookId = b.BookId
    INNER JOIN dbo.Authors a ON a.AuthorId = b.AuthorId
    ORDER BY b.Title;

    IF @ApplyUpdates = 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'Review-only mode: the proposed output was rolled back. Set @ApplyUpdates = 1 only after approval.';
        RETURN;
    END;

    COMMIT TRANSACTION;
    PRINT 'Preview content applied to the eight matched existing catalog books.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

