IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Databases] (
    [Id] int NOT NULL IDENTITY,
    [DbName] nvarchar(max) NULL,
    [Number] nvarchar(max) NULL,
    [Memo] nvarchar(max) NULL,
    [RegisterDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [DisableDate] datetime2 NULL,
    [DeleteDate] datetime2 NULL,
    CONSTRAINT [PK_Databases] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Mobile] nvarchar(max) NULL,
    [Password] nvarchar(max) NULL,
    [FullName] nvarchar(max) NULL,
    [RegisterDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [DisableDate] datetime2 NULL,
    [DeleteDate] datetime2 NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Companies] (
    [Id] int NOT NULL IDENTITY,
    [NationalId] nvarchar(max) NULL,
    [Name] nvarchar(max) NULL,
    [X_Y] nvarchar(max) NULL,
    [Tel] nvarchar(max) NULL,
    [RegisteredByUserId] int NOT NULL,
    [RegisterDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [DisableDate] datetime2 NULL,
    [DeleteDate] datetime2 NULL,
    CONSTRAINT [PK_Companies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Companies_Users_RegisteredByUserId] FOREIGN KEY ([RegisteredByUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserCompanies] (
    [Id] int NOT NULL IDENTITY,
    [Mobile] nvarchar(max) NULL,
    [CompanyId] int NOT NULL,
    [DatabaseId] int NOT NULL,
    [RegisterDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [DisableDate] datetime2 NULL,
    [DeleteDate] datetime2 NULL,
    [IsOwner] bit NOT NULL,
    [CanSelect] bit NOT NULL,
    [CanInsert] bit NOT NULL,
    [CanUpdate] bit NOT NULL,
    [CanDelete] bit NOT NULL,
    CONSTRAINT [PK_UserCompanies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserCompanies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserCompanies_Databases_DatabaseId] FOREIGN KEY ([DatabaseId]) REFERENCES [Databases] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_Companies_RegisteredByUserId] ON [Companies] ([RegisteredByUserId]);
GO

CREATE INDEX [IX_UserCompanies_CompanyId] ON [UserCompanies] ([CompanyId]);
GO

CREATE INDEX [IX_UserCompanies_DatabaseId] ON [UserCompanies] ([DatabaseId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260420110858_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [CompanyDatabases] (
    [Id] int NOT NULL IDENTITY,
    [CompanyId] int NOT NULL,
    [ServerName] nvarchar(200) NULL,
    [DatabaseName] nvarchar(200) NULL,
    [UserId] nvarchar(max) NULL,
    [Password] nvarchar(max) NULL,
    [ConnectionString] nvarchar(1000) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_CompanyDatabases] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CompanyDatabases_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_CompanyDatabases_CompanyId] ON [CompanyDatabases] ([CompanyId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260420115408_AddCompanyDatabases', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

EXEC sp_rename N'[Companies].[X_Y]', N'Y', N'COLUMN';
GO

ALTER TABLE [Companies] ADD [X] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505005838_AddXandYToCompany', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [UserTokens] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [TokenHash] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsRevoked] bit NOT NULL,
    CONSTRAINT [PK_UserTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_UserTokens_UserId] ON [UserTokens] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505012325_AddUserTokensAndLocation', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [UserCompanies] DROP CONSTRAINT [FK_UserCompanies_Databases_DatabaseId];
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserCompanies]') AND [c].[name] = N'DatabaseId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [UserCompanies] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [UserCompanies] ALTER COLUMN [DatabaseId] int NULL;
GO

ALTER TABLE [UserCompanies] ADD CONSTRAINT [FK_UserCompanies_Databases_DatabaseId] FOREIGN KEY ([DatabaseId]) REFERENCES [Databases] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505031853_MakeDatabaseIdNullable', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Companies] DROP CONSTRAINT [FK_Companies_Users_RegisteredByUserId];
GO

ALTER TABLE [CompanyDatabases] DROP CONSTRAINT [FK_CompanyDatabases_Companies_CompanyId];
GO

ALTER TABLE [UserCompanies] DROP CONSTRAINT [FK_UserCompanies_Companies_CompanyId];
GO

ALTER TABLE [UserCompanies] DROP CONSTRAINT [FK_UserCompanies_Databases_DatabaseId];
GO

ALTER TABLE [UserTokens] DROP CONSTRAINT [FK_UserTokens_Users_UserId];
GO

ALTER TABLE [UserTokens] DROP CONSTRAINT [PK_UserTokens];
GO

ALTER TABLE [Users] DROP CONSTRAINT [PK_Users];
GO

ALTER TABLE [UserCompanies] DROP CONSTRAINT [PK_UserCompanies];
GO

ALTER TABLE [Databases] DROP CONSTRAINT [PK_Databases];
GO

ALTER TABLE [CompanyDatabases] DROP CONSTRAINT [PK_CompanyDatabases];
GO

ALTER TABLE [Companies] DROP CONSTRAINT [PK_Companies];
GO

EXEC sp_rename N'[UserTokens]', N'tbl_UserToken';
GO

EXEC sp_rename N'[Users]', N'tbl_User';
GO

EXEC sp_rename N'[UserCompanies]', N'tbl_UserCompany';
GO

EXEC sp_rename N'[Databases]', N'tbl_Databases';
GO

EXEC sp_rename N'[CompanyDatabases]', N'tbl_CompanyDatabases';
GO

EXEC sp_rename N'[Companies]', N'tbl_Company';
GO

EXEC sp_rename N'[tbl_UserToken].[IX_UserTokens_UserId]', N'IX_tbl_UserToken_UserId', N'INDEX';
GO

EXEC sp_rename N'[tbl_UserCompany].[IX_UserCompanies_DatabaseId]', N'IX_tbl_UserCompany_DatabaseId', N'INDEX';
GO

EXEC sp_rename N'[tbl_UserCompany].[IX_UserCompanies_CompanyId]', N'IX_tbl_UserCompany_CompanyId', N'INDEX';
GO

EXEC sp_rename N'[tbl_CompanyDatabases].[IX_CompanyDatabases_CompanyId]', N'IX_tbl_CompanyDatabases_CompanyId', N'INDEX';
GO

EXEC sp_rename N'[tbl_Company].[IX_Companies_RegisteredByUserId]', N'IX_tbl_Company_RegisteredByUserId', N'INDEX';
GO

ALTER TABLE [tbl_UserToken] ADD CONSTRAINT [PK_tbl_UserToken] PRIMARY KEY ([Id]);
GO

ALTER TABLE [tbl_User] ADD CONSTRAINT [PK_tbl_User] PRIMARY KEY ([Id]);
GO

ALTER TABLE [tbl_UserCompany] ADD CONSTRAINT [PK_tbl_UserCompany] PRIMARY KEY ([Id]);
GO

ALTER TABLE [tbl_Databases] ADD CONSTRAINT [PK_tbl_Databases] PRIMARY KEY ([Id]);
GO

ALTER TABLE [tbl_CompanyDatabases] ADD CONSTRAINT [PK_tbl_CompanyDatabases] PRIMARY KEY ([Id]);
GO

ALTER TABLE [tbl_Company] ADD CONSTRAINT [PK_tbl_Company] PRIMARY KEY ([Id]);
GO

ALTER TABLE [tbl_Company] ADD CONSTRAINT [FK_tbl_Company_tbl_User_RegisteredByUserId] FOREIGN KEY ([RegisteredByUserId]) REFERENCES [tbl_User] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [tbl_CompanyDatabases] ADD CONSTRAINT [FK_tbl_CompanyDatabases_tbl_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [tbl_Company] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [tbl_UserCompany] ADD CONSTRAINT [FK_tbl_UserCompany_tbl_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [tbl_Company] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [tbl_UserCompany] ADD CONSTRAINT [FK_tbl_UserCompany_tbl_Databases_DatabaseId] FOREIGN KEY ([DatabaseId]) REFERENCES [tbl_Databases] ([Id]);
GO

ALTER TABLE [tbl_UserToken] ADD CONSTRAINT [FK_tbl_UserToken_tbl_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [tbl_User] ([Id]) ON DELETE CASCADE;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260511213349_CreateAllTables', N'8.0.0');
GO

COMMIT;
GO

