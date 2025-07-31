namespace EmailGraphAPI.Interface {
    internal interface IFilesOperations {
        Task SaveIdsToFileAsync(string id);
        Task<List<string>> GetSavedIdsAsync();
    }
}
