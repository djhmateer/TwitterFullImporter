CREATE TABLE [dbo].[UserFriends] (
    [UserFriendsID]         INT NOT NULL,
    [UserID]                INT NOT NULL,
    [UserIDThatIsTheFriend] INT NOT NULL,
    CONSTRAINT [PK_UserFriends] PRIMARY KEY CLUSTERED ([UserFriendsID] ASC)
);



