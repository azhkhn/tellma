﻿CREATE TYPE [dbo].[RoleMembershipList] AS TABLE
(
	[Index]			INT				DEFAULT 0,
	[HeaderIndex]	INT				DEFAULT 0,
    PRIMARY KEY CLUSTERED ([Index], [HeaderIndex]),
	[Id]			INT NOT NULL	DEFAULT 0,
	[UserId]		INT NULL,
	[RoleId]		INT NULL,
	[Memo]			NVARCHAR(255) NULL
);