namespace AAExamManagementSystem.Services;

public enum ServiceStatus
{
    Success,
    NotFound,
    Invalid
}

public class ServiceResult
{
    public ServiceStatus Status { get; init; }
    public string? Error { get; init; }

    public bool Succeeded => Status == ServiceStatus.Success;

    public static ServiceResult Success() => new() { Status = ServiceStatus.Success };
    public static ServiceResult NotFound() => new() { Status = ServiceStatus.NotFound };
    public static ServiceResult Invalid(string error) => new() { Status = ServiceStatus.Invalid, Error = error };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Success(T value) => new() { Status = ServiceStatus.Success, Value = value };
    public new static ServiceResult<T> Invalid(string error) => new() { Status = ServiceStatus.Invalid, Error = error };
}
