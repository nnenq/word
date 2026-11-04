CREATE TABLE [AuditLog] (
    [Id] int NOT NULL IDENTITY,
    [At] datetime2 NOT NULL,
    [UserId] int NULL,
    [UserName] nvarchar(100) NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [Details] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_AuditLog] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Disciplines] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(150) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Disciplines] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [StudentGroups] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_StudentGroups] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Login] nvarchar(50) NOT NULL,
    [PasswordHash] nvarchar(200) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [Role] nvarchar(20) NOT NULL,
    [Email] nvarchar(100) NULL,
    [IsBlocked] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Topics] (
    [Id] int NOT NULL IDENTITY,
    [DisciplineId] int NOT NULL,
    [Title] nvarchar(150) NOT NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_Topics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Topics_Disciplines_DisciplineId] FOREIGN KEY ([DisciplineId]) REFERENCES [Disciplines] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [GroupMemberships] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [GroupId] int NOT NULL,
    CONSTRAINT [PK_GroupMemberships] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GroupMemberships_StudentGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [StudentGroups] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GroupMemberships_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Lectures] (
    [Id] int NOT NULL IDENTITY,
    [TopicId] int NOT NULL,
    [Number] int NOT NULL,
    [Title] nvarchar(150) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Order] int NOT NULL,
    [MaterialFile] nvarchar(200) NULL,
    [MaterialName] nvarchar(200) NULL,
    [MaterialUpdatedAt] datetime2 NULL,
    [MaterialVersion] int NOT NULL,
    [RequireControl] bit NOT NULL,
    [RequireControlCorrect] bit NOT NULL,
    [RequireReview] bit NOT NULL,
    CONSTRAINT [PK_Lectures] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Lectures_Topics_TopicId] FOREIGN KEY ([TopicId]) REFERENCES [Topics] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ControlQuestions] (
    [Id] int NOT NULL IDENTITY,
    [LectureId] int NOT NULL,
    [Text] nvarchar(1000) NOT NULL,
    [Type] int NOT NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_ControlQuestions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ControlQuestions_Lectures_LectureId] FOREIGN KEY ([LectureId]) REFERENCES [Lectures] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LectureProgress] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [LectureId] int NOT NULL,
    [Status] int NULL,
    [FirstOpenedAt] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [ExtraAttempts] int NOT NULL,
    CONSTRAINT [PK_LectureProgress] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LectureProgress_Lectures_LectureId] FOREIGN KEY ([LectureId]) REFERENCES [Lectures] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_LectureProgress_Users_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Tests] (
    [Id] int NOT NULL IDENTITY,
    [LectureId] int NOT NULL,
    [QuestionCount] int NOT NULL,
    [MaxAttempts] int NOT NULL,
    [PassPercent] int NOT NULL,
    [TimeLimitMinutes] int NOT NULL,
    [ShuffleQuestions] bit NOT NULL,
    [ShuffleOptions] bit NOT NULL,
    [AllowRetry] bit NOT NULL,
    CONSTRAINT [PK_Tests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Test_Attempts] CHECK (MaxAttempts >= 0),
    CONSTRAINT [CK_Test_PassPercent] CHECK (PassPercent BETWEEN 1 AND 100),
    CONSTRAINT [FK_Tests_Lectures_LectureId] FOREIGN KEY ([LectureId]) REFERENCES [Lectures] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [StudentAnswers] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [ControlQuestionId] int NOT NULL,
    [Answer] nvarchar(4000) NOT NULL,
    [Status] int NOT NULL,
    [TeacherComment] nvarchar(1000) NULL,
    [AnsweredAt] datetime2 NOT NULL,
    [CheckedAt] datetime2 NULL,
    [CheckedById] int NULL,
    CONSTRAINT [PK_StudentAnswers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentAnswers_ControlQuestions_ControlQuestionId] FOREIGN KEY ([ControlQuestionId]) REFERENCES [ControlQuestions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StudentAnswers_Users_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [TestAttempts] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [TestId] int NOT NULL,
    [StartedAt] datetime2 NOT NULL,
    [Deadline] datetime2 NULL,
    [FinishedAt] datetime2 NULL,
    [QuestionsJson] nvarchar(max) NOT NULL,
    [AnswersJson] nvarchar(max) NOT NULL,
    [Correct] int NOT NULL,
    [Total] int NOT NULL,
    [Percent] int NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_TestAttempts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TestAttempts_Tests_TestId] FOREIGN KEY ([TestId]) REFERENCES [Tests] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TestAttempts_Users_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [TestQuestions] (
    [Id] int NOT NULL IDENTITY,
    [TestId] int NOT NULL,
    [Text] nvarchar(1000) NOT NULL,
    [Type] int NOT NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_TestQuestions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TestQuestions_Tests_TestId] FOREIGN KEY ([TestId]) REFERENCES [Tests] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AnswerOptions] (
    [Id] int NOT NULL IDENTITY,
    [ControlQuestionId] int NULL,
    [TestQuestionId] int NULL,
    [Text] nvarchar(500) NOT NULL,
    [MatchText] nvarchar(500) NULL,
    [IsCorrect] bit NOT NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_AnswerOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AnswerOption_Owner] CHECK ((ControlQuestionId IS NOT NULL AND TestQuestionId IS NULL) OR (ControlQuestionId IS NULL AND TestQuestionId IS NOT NULL)),
    CONSTRAINT [FK_AnswerOptions_ControlQuestions_ControlQuestionId] FOREIGN KEY ([ControlQuestionId]) REFERENCES [ControlQuestions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AnswerOptions_TestQuestions_TestQuestionId] FOREIGN KEY ([TestQuestionId]) REFERENCES [TestQuestions] ([Id])
);
GO


CREATE INDEX [IX_AnswerOptions_ControlQuestionId] ON [AnswerOptions] ([ControlQuestionId]);
GO


CREATE INDEX [IX_AnswerOptions_TestQuestionId] ON [AnswerOptions] ([TestQuestionId]);
GO


CREATE INDEX [IX_AuditLog_At] ON [AuditLog] ([At]);
GO


CREATE INDEX [IX_ControlQuestions_LectureId] ON [ControlQuestions] ([LectureId]);
GO


CREATE INDEX [IX_GroupMemberships_GroupId] ON [GroupMemberships] ([GroupId]);
GO


CREATE UNIQUE INDEX [IX_GroupMemberships_UserId_GroupId] ON [GroupMemberships] ([UserId], [GroupId]);
GO


CREATE INDEX [IX_LectureProgress_LectureId] ON [LectureProgress] ([LectureId]);
GO


CREATE UNIQUE INDEX [IX_LectureProgress_StudentId_LectureId] ON [LectureProgress] ([StudentId], [LectureId]);
GO


CREATE INDEX [IX_Lectures_TopicId] ON [Lectures] ([TopicId]);
GO


CREATE INDEX [IX_StudentAnswers_ControlQuestionId] ON [StudentAnswers] ([ControlQuestionId]);
GO


CREATE UNIQUE INDEX [IX_StudentAnswers_StudentId_ControlQuestionId] ON [StudentAnswers] ([StudentId], [ControlQuestionId]);
GO


CREATE UNIQUE INDEX [IX_StudentGroups_Name] ON [StudentGroups] ([Name]);
GO


CREATE INDEX [IX_TestAttempts_StudentId_TestId] ON [TestAttempts] ([StudentId], [TestId]);
GO


CREATE INDEX [IX_TestAttempts_TestId] ON [TestAttempts] ([TestId]);
GO


CREATE INDEX [IX_TestQuestions_TestId] ON [TestQuestions] ([TestId]);
GO


CREATE UNIQUE INDEX [IX_Tests_LectureId] ON [Tests] ([LectureId]);
GO


CREATE INDEX [IX_Topics_DisciplineId] ON [Topics] ([DisciplineId]);
GO


CREATE UNIQUE INDEX [IX_Users_Login] ON [Users] ([Login]);
GO


