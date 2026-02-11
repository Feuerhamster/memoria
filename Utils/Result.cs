namespace Memoria.Utils;

public class Result<T> {
	public bool IsOk { get; private set; }
	public T Value { get; private set; }
	public Exception Exception { get; private set; }
	
	public bool IsFailed => !IsOk;

	public Result(T value) {
		this.IsOk = true;
		this.Value = value;
	}

	public Result(Exception exception) {
		this.IsOk = false;
		this.Exception = exception;
	}
}