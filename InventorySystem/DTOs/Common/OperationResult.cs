using System.Collections.Generic;

namespace InventorySystem.DTOs.Common
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();

        public static OperationResult Ok(string message = "Operation completed successfully.") =>
            new OperationResult { Success = true, Message = message };

        public static OperationResult Fail(string error) =>
            new OperationResult { Success = false, Message = error, Errors = new List<string> { error } };

        public static OperationResult Fail(List<string> errors) =>
            new OperationResult { Success = false, Message = "Validation failed.", Errors = errors };
    }

    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; set; }

        public static OperationResult<T> Ok(T data, string message = "Operation completed successfully.") =>
            new OperationResult<T> { Success = true, Data = data, Message = message };

        public new static OperationResult<T> Fail(string error) =>
            new OperationResult<T> { Success = false, Message = error, Errors = new List<string> { error } };

        public new static OperationResult<T> Fail(List<string> errors) =>
            new OperationResult<T> { Success = false, Message = "Validation failed.", Errors = errors };
    }
}
