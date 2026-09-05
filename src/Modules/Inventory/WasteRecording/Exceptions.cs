namespace ALKAROS.Inventory.WasteRecording;

public class WasteRecordingException : Exception
{
    public WasteRecordingException(string message) : base(message) { }
    public WasteRecordingException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class InvalidWasteQuantityException : WasteRecordingException
{
    public InvalidWasteQuantityException(string message) : base(message) { }
}

public sealed class InvalidWasteReasonException : WasteRecordingException
{
    public InvalidWasteReasonException(string message) : base(message) { }
}

public sealed class UnauthorizedWasteRecorderException : WasteRecordingException
{
    public UnauthorizedWasteRecorderException(string message) : base(message) { }
}

public sealed class IncompatibleWasteUnitException : WasteRecordingException
{
    public IncompatibleWasteUnitException(string message) : base(message) { }
}

public sealed class InsufficientStockForWasteException : WasteRecordingException
{
    public InsufficientStockForWasteException(string message) : base(message) { }
}

public sealed class WasteItemNotFoundException : WasteRecordingException
{
    public WasteItemNotFoundException(string message) : base(message) { }
}

public sealed class WasteLocationNotFoundException : WasteRecordingException
{
    public WasteLocationNotFoundException(string message) : base(message) { }
}
