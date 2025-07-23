// Модели данных для API
public record ExecuteScriptRequest(string Script);
public record CreateFolderRequest(string Path, string Name);
public record SaveFileRequest(string FilePath, string Content);
public record BatchTask(string Name, string Type, string Script);
public record BatchExecuteRequest(List<BatchTask> Tasks);