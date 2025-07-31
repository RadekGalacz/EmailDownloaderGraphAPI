namespace EmailGraphAPI.Interface {
   internal interface IFoldersOperations {
        string SubFolderPathName { get; set; }
        void CreateUniqueFolderPath(string basePath, string subject);
        void CreateFolderForEmails();
        Task SaveEmailsToSubfolders(Stream content);
    }
}
