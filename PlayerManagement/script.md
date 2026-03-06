-- Create PlayerLogin table
CREATE TABLE PlayerLogin (
    PlayerLoginId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlayerId UNIQUEIDENTIFIER NOT NULL,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(32) NOT NULL, -- MD5 hash
    IsAdmin BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    LastLoginDate DATETIME NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedDate DATETIME NULL,
    CONSTRAINT FK_PlayerLogin_Player FOREIGN KEY (PlayerId) REFERENCES Player(PlayerId)
);

CREATE INDEX IX_PlayerLogin_Username ON PlayerLogin(Username);
CREATE INDEX IX_PlayerLogin_PlayerId ON PlayerLogin(PlayerId);

-- Seed admin accounts
INSERT INTO Player (PlayerId, FirstName, LastName, CardNumber, CreationDate, Status)
VALUES 
    (NEWID(), 'System', 'Administrator', 'admin', GETDATE(), 1),
    (NEWID(), 'System', 'Admin', 'sysadmin', GETDATE(), 1);

-- Insert admin logins (password = username, MD5 hashed)
DECLARE @AdminPlayerId UNIQUEIDENTIFIER = (SELECT PlayerId FROM Player WHERE CardNumber = 'admin');
DECLARE @SysAdminPlayerId UNIQUEIDENTIFIER = (SELECT PlayerId FROM Player WHERE CardNumber = 'sysadmin');

INSERT INTO PlayerLogin (PlayerLoginId, PlayerId, Username, PasswordHash, IsAdmin, IsActive, CreatedDate)
VALUES 
    (NEWID(), @AdminPlayerId, 'admin', '21232f297a57a5a743894a0e4a801fc3', 1, 1, GETDATE()), -- admin
    (NEWID(), @SysAdminPlayerId, 'sysadmin', '86fc480db8a2bcbcb30b896c61301aa3', 1, 1, GETDATE()); -- sysadmin
