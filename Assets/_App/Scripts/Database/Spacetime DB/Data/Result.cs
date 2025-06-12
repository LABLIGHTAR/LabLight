using System;
public class ErrorDetails
{
    public string Code { get; set; }
    public string Message { get; set; }

    public ErrorDetails(string code, string message)
    {
        Code = code;
        Message = message;
    }
}

public class Result<T>
{
    public bool Success { get; private set; }
    public T Data { get; private set; }
    public ErrorDetails Error { get; private set; }

    public static Result<T> CreateSuccess(T data)
    {
        return new Result<T> { Success = true, Data = data };
    }

    public static Result<T> CreateFailure(string errorCode, string errorMessage)
    {
        return new Result<T> { Success = false, Error = new ErrorDetails(errorCode, errorMessage) };
    }
}

public class ResultVoid
{
    public bool Success { get; private set; }
    public ErrorDetails Error { get; private set; }

    public static ResultVoid CreateSuccess()
    {
        return new ResultVoid { Success = true };
    }

    public static ResultVoid CreateFailure(string errorCode, string errorMessage)
    {
        return new ResultVoid { Success = false, Error = new ErrorDetails(errorCode, errorMessage) };
    }
}